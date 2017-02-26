using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;


namespace MahAppsNotThreadSafe
{
	/// <summary>
	/// Implementation of a <see cref="ResourceDictionary"/> that enables Thread-Local
	/// <see cref="ResourceDictionary.MergedDictionaries"/>. One object must be used as the
	/// <see cref="Application"/> <see cref="Application.Resources"/> instance: that instance
	/// will monitor all dictionaries that are merged into its MergedDictionaries, and get
	/// the <see cref="ResourceDictionary.Source"/> <see cref="Uri"/> of each added dictionary.
	/// Notice that this must be non-null and not empty to enable synchronizing dictionaries.
	/// Create other instances by using this class in the XAML for controls that may be run on
	/// other Threads. E.g: <c>&lt;Window.Resources&gt;&lt;common:ThreadLocalResourceDictionary&gt;...</c>.
	/// When each such instance is contructed it will have a Thread-Local copy of all of the main
	/// dictionaries added to its MergedDictionaries. Each instance created on a given Thread will
	/// share the actual Thread-Local instances. If changes are made to the main dictionary, all
	/// dictionaries will be updated. The main instance, and that instance alone, must have the
	/// <see cref="IsAppResources"/> property set to true. Notice also: the main Application.Resources
	/// instance will retain any MergedDictionaries --- in the normal way. And then each Thread-Local
	/// instance that is not on the main Thread will still get a Thread-Local copy of all merged
	/// dictionaries, which then will "hide" the main instances. In Desintime mode, dictionaries
	/// are not copied.
	/// </summary>
	[Localizability(LocalizationCategory.Ignore)]
	[Ambient]
	[UsableDuringInitialization(true)]
	public class ThreadLocalResourceDictionary : ResourceDictionary
	{
		private static readonly object staticThreadLock = new object();

		private static readonly ThreadLocal<MultiDictionary<Dispatcher, WeakReference<ThreadLocalResourceDictionary>>>
				threadLocalDictionaries
						= new ThreadLocal<MultiDictionary<Dispatcher, WeakReference<ThreadLocalResourceDictionary>>>(true);

		private static Dispatcher appResourcesDispatcher;
		private static ResourceDictionary appResourcesDictionary;
		private static Uri[] appResources;


		/// <summary>
		/// Enumerates the dictionary, prunes any collected references, and also prunes the
		/// <see cref="appResourcesDictionary"/> and <see cref="appResourcesDispatcher"/>
		/// if present; and returns the rest. Not synchronized.
		/// </summary>
		/// <param name="threadLocal"></param>
		/// <param name="dispatcher"></param>
		/// <returns>Not null.</returns>
		private static ThreadLocalResourceDictionary[] getPrunedThreadLocalDictionaries(
				MultiDictionary<Dispatcher, WeakReference<ThreadLocalResourceDictionary>> threadLocal,
				Dispatcher dispatcher) {
			if (threadLocal.ContainsKey(ThreadLocalResourceDictionary.appResourcesDispatcher))
				threadLocal.RemoveKey(ThreadLocalResourceDictionary.appResourcesDispatcher);
			List<WeakReference<ThreadLocalResourceDictionary>> weakValues = threadLocal.GetAllValues(dispatcher);
			List<ThreadLocalResourceDictionary> result = new List<ThreadLocalResourceDictionary>();
			foreach (WeakReference<ThreadLocalResourceDictionary> weakValue in weakValues) {
				if (weakValue.TryGetTarget(out ThreadLocalResourceDictionary value)
						&& (value != ThreadLocalResourceDictionary.appResourcesDictionary))
					result.Add(value);
				else
					threadLocal.RemoveValue(dispatcher, weakValue);
			}
			return result.ToArray();
		}

		private static void checkMergedUris(
				Uri[] removedUris,
				Uri[] allResources,
				params ThreadLocalResourceDictionary[] targets) {
			foreach (Uri removedUri in removedUris) {
				foreach (ThreadLocalResourceDictionary target in targets) {
					ResourceDictionary mergedDictionary
							= target.MergedDictionaries.FirstOrDefault(rd => rd.Source?.Equals(removedUri) ?? false);
					if (mergedDictionary != null)
						target.MergedDictionaries.Remove(mergedDictionary);
				}
			}
			List<ResourceDictionary> merge = new List<ResourceDictionary>(allResources.Length);
			foreach (Uri mergedUri in allResources) {
				bool foundInstance = false;
				foreach (ThreadLocalResourceDictionary target in targets) {
					ResourceDictionary targetDictionary
							= target.MergedDictionaries.FirstOrDefault(rd => rd.Source?.Equals(mergedUri) ?? false);
					if (targetDictionary != null) {
						merge.Add(targetDictionary);
						foundInstance = true;
						break;
					}
				}
				if (!foundInstance)
					merge.Add(
							new ResourceDictionary {
								Source = mergedUri
							});
			}
			int i = 0;
			foreach (ResourceDictionary mergedRd in merge) {
				foreach (ThreadLocalResourceDictionary target in targets) {
					ResourceDictionary targetDictionary
							= target.MergedDictionaries.FirstOrDefault(rd => rd.Source?.Equals(mergedRd.Source) ?? false);
					if (targetDictionary == null)
						target.MergedDictionaries.Insert(i, mergedRd);
					else if (target.MergedDictionaries.IndexOf(targetDictionary) != i) {
						target.MergedDictionaries.Remove(targetDictionary);
						target.MergedDictionaries.Insert(i, targetDictionary);
					}
				}
				++i;
			}
		}


		private bool isAppResources;


		/// <summary>
		/// Constructor: the invoking Thread must have a Dispatcher. An Error is Traced if not;
		/// and <see cref="Debug.Fail(string)"/> is invoked.
		/// </summary>
		public ThreadLocalResourceDictionary() {
			if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
				return;
			Dispatcher dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
			if (dispatcher == null) {
				Trace.TraceError($"{GetType().Name} constructor must have a Dispatcher.");
				Debug.Fail($"{GetType().Name} constructor must have a Dispatcher.");
				return;
			}
			Uri[] mergeUris;
			ThreadLocalResourceDictionary[] targets;
			lock (ThreadLocalResourceDictionary.staticThreadLock) {
				if (dispatcher == ThreadLocalResourceDictionary.appResourcesDispatcher)
					return;
				MultiDictionary<Dispatcher, WeakReference<ThreadLocalResourceDictionary>> threadLocal;
				if (!ThreadLocalResourceDictionary.threadLocalDictionaries.IsValueCreated)
					ThreadLocalResourceDictionary.threadLocalDictionaries.Value
							= threadLocal
									= new MultiDictionary<Dispatcher, WeakReference<ThreadLocalResourceDictionary>>();
				else
					threadLocal = ThreadLocalResourceDictionary.threadLocalDictionaries.Value;
				threadLocal.AddValue(
						dispatcher,
						new WeakReference<ThreadLocalResourceDictionary>(this));
				if (ThreadLocalResourceDictionary.appResources == null)
					return;
				mergeUris = new Uri[ThreadLocalResourceDictionary.appResources.Length];
				ThreadLocalResourceDictionary.appResources.CopyTo(mergeUris, 0);
				targets = ThreadLocalResourceDictionary.getPrunedThreadLocalDictionaries(threadLocal, dispatcher);
			}
			ThreadLocalResourceDictionary.checkMergedUris(new Uri[0], mergeUris, targets);
		}


		private void handleAppResourcesMergedDictionariesChanged(object sender, NotifyCollectionChangedEventArgs evt)
			=> updateAppResourcesList();

		private void updateAppResourcesList() {
			if (!isAppResources || (LicenseManager.UsageMode == LicenseUsageMode.Designtime))
				return;
			lock (ThreadLocalResourceDictionary.staticThreadLock) {
				Dictionary<Dispatcher, ThreadLocalResourceDictionary[]> allTargets
						= new Dictionary<Dispatcher, ThreadLocalResourceDictionary[]>();
				foreach (MultiDictionary<Dispatcher, WeakReference<ThreadLocalResourceDictionary>> threadLocal
						in ThreadLocalResourceDictionary.threadLocalDictionaries.Values) {
					foreach (Dispatcher key in threadLocal.Keys) {
						ThreadLocalResourceDictionary[] targets
								= ThreadLocalResourceDictionary.getPrunedThreadLocalDictionaries(threadLocal, key);
						if (targets.Length > 0)
							allTargets[key] = targets;
					}
				}
				if (allTargets.Count == 0)
					return;

				List<Uri> oldAppResources = new List<Uri>(ThreadLocalResourceDictionary.appResources);
				Uri[] allResources
						= MergedDictionaries.TakeWhile(
								rd => {
									if (string.IsNullOrWhiteSpace(rd.Source?.ToString())) {
										Trace.TraceError(
												$"{GetType().Name} "
												+ $"merged ResourceDictionary has null or whitespace Uri: {rd}");
										Debug.Fail(
												$"{GetType().Name} "
												+ $"merged ResourceDictionary has null or whitespace Uri: {rd}");
										return false;
									}
									return true;
								}).Select(rd => rd.Source).ToArray();
				foreach (Uri mergedUri in allResources) {
					oldAppResources.Remove(mergedUri);
				}
				Uri[] removeUris = oldAppResources.ToArray();
				ThreadLocalResourceDictionary.appResources = allResources;
				foreach (KeyValuePair<Dispatcher, ThreadLocalResourceDictionary[]> targets in allTargets) {
					targets.Key.InvokeAsync(
							() => ThreadLocalResourceDictionary.checkMergedUris(
									removeUris,
									allResources,
									targets.Value));
				}
			}
		}


		/// <summary>
		/// Must only be set on the instance declared as the <see cref="Application.Resources"/>
		/// instance; and MUST be set before creating any other instances. Enables synchronizing
		/// the <see cref="ResourceDictionary.MergedDictionaries"/> on this instance with all other
		/// instances. The setter will THROW if the value is False, or if an instance has already
		/// been set.
		/// </summary>
		public bool IsAppResources {
			get { return isAppResources; }
			set {
				if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) {
					isAppResources = true;
					return;
				}
				if (!value)
					throw new InvalidOperationException(
							$"{GetType().Name}.{nameof(ThreadLocalResourceDictionary.IsAppResources)} "
							+ "may not be explicitly set false.");
				Dispatcher dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
				if (dispatcher == null)
					throw new InvalidOperationException(
							$"{GetType().Name}.{nameof(ThreadLocalResourceDictionary.IsAppResources)} "
							+ "instance must have a Dispatcher.");
				lock (ThreadLocalResourceDictionary.staticThreadLock) {
					if (ThreadLocalResourceDictionary.appResourcesDictionary != null)
						throw new InvalidOperationException(
								$"{GetType().Name}.{nameof(ThreadLocalResourceDictionary.IsAppResources)} "
								+ "has already been set.");
					isAppResources = true;
					ThreadLocalResourceDictionary.appResourcesDispatcher = dispatcher;
					ThreadLocalResourceDictionary.appResourcesDictionary = this;
					ThreadLocalResourceDictionary.appResources = new Uri[0];
					if (MergedDictionaries is INotifyCollectionChanged notifyCollection)
						notifyCollection.CollectionChanged += handleAppResourcesMergedDictionariesChanged;
					else {
						Trace.TraceError(
								$"{GetType().Name}.{nameof(ThreadLocalResourceDictionary.IsAppResources)} "
								+ "expecting ResourceDictionary.MergedDictionaries to be observable.");
						Debug.Fail(
								$"{GetType().Name}.{nameof(ThreadLocalResourceDictionary.IsAppResources)} "
								+ "expecting ResourceDictionary.MergedDictionaries to be observable.");
					}
				}
			}
		}
	}
}
