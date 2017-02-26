using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;


namespace MahAppsNotThreadSafe
{
	/// <summary>
	/// Static utilities for working with Windows.
	/// </summary>
	public static class WindowHelper
	{
		/// <summary>
		/// Params for the STA Thread new Window methods.
		/// </summary>
		/// <typeparam name="TWindow"></typeparam>
		/// <typeparam name="TArgs"></typeparam>
		private class StaWindowThreadParams<TWindow, TArgs> : IDisposable
			where TWindow : Window
		{
			private TWindow window;
			private Exception error;


			/// <summary>
			/// Constructor. Creates the <see cref="Gate"/>.
			/// </summary>
			/// <param name="constructor">Must not be null.</param>
			/// <param name="constructorArgs">Optional.</param>
			internal StaWindowThreadParams(
					Func<TArgs, TWindow> constructor,
					Func<TArgs> constructorArgs) {
				Gate = new AutoResetEvent(false);
				Constructor = constructor;
				ConstructorArgs = constructorArgs;
			}


			/// <summary>
			/// Used to communicate between the invoker and the window's Thread.
			/// </summary>
			internal AutoResetEvent Gate { get; }

			/// <summary>
			/// The window's constructor.
			/// </summary>
			internal Func<TArgs, TWindow> Constructor { get; }

			/// <summary>
			/// Optional args for the <see cref="Constructor"/>.
			/// </summary>
			internal Func<TArgs> ConstructorArgs { get; }

			/// <summary>
			/// The returned new Window.
			/// </summary>
			internal TWindow Window {
				get { return window; }
				set { Interlocked.Exchange(ref window, value); }
			}

			/// <summary>
			/// An error riased on the new Window's Thread
			/// during construction.
			/// </summary>
			internal Exception Error {
				get { return error; }
				set { Interlocked.Exchange(ref error, value); }
			}

			public void Dispose()
				=> Gate.Dispose();
		}


		/// <summary>
		/// Creates a new STA Background Thread, which sets a <c>SynchronizationContext</c>
		/// with a <c>Dispatcher</c>. Then constructs a new <c>Window</c> on the Thread,
		/// using the <c>constructor</c> <c>Func</c> and any <c>constructorArgs</c>. Then
		/// the Thread runs the <c>Dispatcher</c> and this method returns the <c>Window</c>.
		/// The <c>Dispatcher</c> listens for <c>window.Closed</c> and invokes
		/// <c>BeginInvokeShutdown</c>. The <c>constructorArgs</c> is provided as a <c>Func</c>
		/// so that it will be invoked on the <c>Window's</c> Thread: the object you return
		/// is passed to your <c>constructor</c> (and has been created on that Thread). This
		/// may be null.
		/// </summary>
		/// <typeparam name="TWindow">Your Window Type.</typeparam>
		/// <typeparam name="TArgs">Constructor's argument type</typeparam>
		/// <param name="constructor">Construct and return your Window.</param>
		/// <param name="constructorArgs">Optional args for your constructor Func. If this
		/// argument is not null, it will be invoked on the new Thread, and the result
		/// is passed to your <c>constructor</c>. If this is null, your constructor receives
		/// <c>default(TArgs)</c>.</param>
		/// <param name="error">This must be checked. If not null, the result will
		/// be null.</param>
		public static TWindow CreateWindowOnNewStaThread<TWindow, TArgs>(
				Func<TArgs, TWindow> constructor,
				Func<TArgs> constructorArgs,
				out Exception error) where TWindow : Window {
			using (StaWindowThreadParams<TWindow, TArgs> threadParams
					= new StaWindowThreadParams<TWindow, TArgs>(constructor, constructorArgs)) {
				Thread newWindowThread = new Thread(
						tp => {
							if (WindowHelper.trySpawnOnStaThread((StaWindowThreadParams<TWindow, TArgs>) tp))
								Dispatcher.Run();
						});
				newWindowThread.SetApartmentState(ApartmentState.STA);
				newWindowThread.IsBackground = true;
				newWindowThread.Start(threadParams);
				threadParams.Gate.WaitOne();
				error = threadParams.Error;
				return threadParams.Window;
			}
		}

		private static bool trySpawnOnStaThread<TWindow, TArgs>(StaWindowThreadParams<TWindow, TArgs> myParams)
			where TWindow : Window {
			Dispatcher myDispatcher = null;
			try {
				SynchronizationContext.SetSynchronizationContext(
						new DispatcherSynchronizationContext(
								(myDispatcher = Dispatcher.CurrentDispatcher)));
				myParams.Window = myParams.Constructor(
						myParams.ConstructorArgs != null
							? myParams.ConstructorArgs()
							: default(TArgs));
				myParams.Window.Closed += WindowHelper.windowClosed;
			} catch (Exception ex) {
				Trace.TraceError(
						$"{typeof(WindowHelper)} {nameof(WindowHelper.CreateWindowOnNewStaThread)}: "
						+ $"exception within ThreadStart. {ex}");
				if (myParams.Window != null) {
					myParams.Window.Closed -= WindowHelper.windowClosed;
					try {
						myParams.Window.Close();
					} catch {
						// Ignored
					}
				}
				try {
					myDispatcher?.InvokeShutdown();
				} catch {
					// Ignored
				}
				myParams.Window = null;
				myParams.Error = ex;
				return false;
			} finally {
				myParams.Gate.Set();
			}
			return true;
		}

		private static void windowClosed(object sender, EventArgs evt) {
			((Window) sender).Closed -= WindowHelper.windowClosed;
			Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
		}
	}
}
