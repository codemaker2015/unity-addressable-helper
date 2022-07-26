// Resharper disable all

using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace SUP.AddressablesHelper {
    public sealed class AddressablesHelper {
        #region Fields & Properties

        private static AddressablesHelper _instance;
        private readonly Dictionary<Type, List<IResourceLocation>> _iResourcesLocations;
        private static List<AsyncOperationHandle> _asyncOperationHandlesList;

        public static WaitForAddressablesHelper WaitForInit { get; private set; }

        public static AddressablesHelper Instance {
            get {
                if (_instance != null) return _instance;
                Debug.LogError("Please initialize the addressables helper.");
                return null;
            }
        }

        #endregion

        #region Custom functions

        #region Init Functions

        public static void Init(Dictionary<string, Type> labelAndTypes) {
            _instance = new AddressablesHelper(labelAndTypes);
            _asyncOperationHandlesList = new List<AsyncOperationHandle>();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            WaitForInit = new WaitForAddressablesHelper();
        }

        private AddressablesHelper(Dictionary<string, Type> addressableLabelsAndType) {
            _iResourcesLocations = new Dictionary<Type, List<IResourceLocation>>();
            LoadResourcesLocations(addressableLabelsAndType);
        }

        private async void LoadResourcesLocations(Dictionary<string, Type> labelAndTypes) {
            // ReSharper disable once UseDeconstruction
            foreach (var labelAndType in labelAndTypes) {
                var label = labelAndType.Key;
                var type = labelAndType.Value;
                var resourceLocationsList = await Addressables.LoadResourceLocationsAsync(label, type).Task;

                if (resourceLocationsList == null) {
                    Debug.LogError($"No resource locations found for {label}");
                    continue;
                }

                if (_iResourcesLocations.ContainsKey(type)) {
                    _iResourcesLocations[type].AddRange(resourceLocationsList);
                } else {
                    _iResourcesLocations.Add(type, resourceLocationsList.ToList());
                }
            }

            WaitForInit.StopWaiting();
        }

        #endregion

        #region Single Asset Loading

        public WaitForAddressablesHelper LoadAsset<T>(string assetName, Action<T> onComplete, bool doNotDestroyOnLoad = false) {
            if (WaitForInit.keepWaiting) {
                Debug.LogError("Addressables helper is not yet initialized. Check the status using AddressablesHelper.IsReady");
                return null;
            }

            var type = typeof(T);
            if (!_iResourcesLocations.ContainsKey(type)) {
                Debug.LogError($"No asset found of type: {type}");
                return null;
            }

            var waitForAddressablesHelper = new WaitForAddressablesHelper();
            var iResourceLocation = _iResourcesLocations[type].Find(location => location.ToString().Contains(assetName));

            if (iResourceLocation == null) {
                Debug.LogError($"No asset found matching: {assetName}");
                return null;
            }

            var operationHandle = Addressables.LoadAssetAsync<T>(iResourceLocation);
            operationHandle.Completed += handle => {
                var loadedAsset = handle.Result;
                onComplete.Invoke(loadedAsset);
                waitForAddressablesHelper.StopWaiting();

                if (!doNotDestroyOnLoad)
                    _asyncOperationHandlesList.Add(handle);
            };

            return waitForAddressablesHelper;
        }

        #endregion

        #region Multiple Assets Loading

        public WaitForAddressablesHelper LoadAssets<T>(IEnumerable<string> assetNames, Action<IEnumerable<T>> onComplete, bool doNotDestroyOnLoad = false) {
            if (WaitForInit.keepWaiting) {
                Debug.LogError("Addressables helper is not yet initialized. Check the status using AddressablesHelper.IsReady");
                return null;
            }

            var type = typeof(T);
            if (!_iResourcesLocations.ContainsKey(type)) {
                Debug.LogError($"No asset found of type: {type}");
                return null;
            }

            var waitForAddressablesHelper = new WaitForAddressablesHelper();
            var assetNamesArray = assetNames as string[] ?? assetNames.ToArray();
            IEnumerable<IResourceLocation> iResourceLocations = assetNamesArray.SelectMany(name => _iResourcesLocations[type].Where(location => location.ToString().Contains(name))).ToList();

            if (!iResourceLocations.Any()) {
                Debug.LogError($"No assets found matching: {assetNamesArray.Aggregate("", (s1, s2) => s1 + "\n" + s2)}");
                return null;
            }

            var operationHandle = Addressables.LoadAssetsAsync<T>(iResourceLocations, null);
            operationHandle.Completed += handle => {
                var loadedAsset = handle.Result;
                onComplete.Invoke(loadedAsset);
                waitForAddressablesHelper.StopWaiting();
                
                if (!doNotDestroyOnLoad)
                    _asyncOperationHandlesList.Add(handle);
            };
            return waitForAddressablesHelper;
        }

        #endregion

        #endregion

        #region Default Static Functions

        #region By Address

        public static WaitForAddressablesHelper LoadAssetByAddress<T>(string assetAddress, Action<T> onComplete, bool doNotDestroyOnLoad = false) {
            var waitForAddressablesHelper = new WaitForAddressablesHelper();
            var operationHandle = Addressables.LoadAssetAsync<T>(assetAddress);
            operationHandle.Completed += handle => {
                var loadedAsset = handle.Result;
                onComplete.Invoke(loadedAsset);
                waitForAddressablesHelper.StopWaiting();
                
                if (!doNotDestroyOnLoad)
                    _asyncOperationHandlesList.Add(handle);
            };

            return waitForAddressablesHelper;
        }

        public static WaitForAddressablesHelper LoadAssetsByAddress<T>(IEnumerable<string> assetAddresses, Action<IEnumerable<T>> onComplete, bool doNotDestroyOnLoad = false) {
            var waitForAddressablesHelper = new WaitForAddressablesHelper();
            var operationHandle = Addressables.LoadAssetsAsync<T>(assetAddresses, null, Addressables.MergeMode.Union);
            operationHandle.Completed += handle => {
                var loadedAsset = handle.Result;
                onComplete.Invoke(loadedAsset);
                waitForAddressablesHelper.StopWaiting();
                
                if (!doNotDestroyOnLoad)
                    _asyncOperationHandlesList.Add(handle);
            };

            return waitForAddressablesHelper;
        }

        #endregion

        #region By Labels

        public static WaitForAddressablesHelper LoadAssetsByLabel<T>(string label, Action<IEnumerable<T>> onComplete, bool doNotDestroyOnLoad = false) {
            return LoadAssetsByLabels(new[] {label}, onComplete);
        }

        public static WaitForAddressablesHelper LoadAssetsByLabels<T>(IEnumerable<string> label, Action<IEnumerable<T>> onComplete, bool doNotDestroyOnLoad = false) {
            var waitForAddressablesHelper = new WaitForAddressablesHelper();
            var operationHandle = Addressables.LoadAssetsAsync<T>(label, null, Addressables.MergeMode.Union);
            operationHandle.Completed += handle => {
                var loadedAsset = handle.Result;
                onComplete.Invoke(loadedAsset);
                waitForAddressablesHelper.StopWaiting();
                
                if (!doNotDestroyOnLoad)
                    _asyncOperationHandlesList.Add(handle);
            };

            return waitForAddressablesHelper;
        }

        #endregion

        #endregion

        #region Release handles on scene unload

        private static void OnSceneUnloaded(Scene scene) {
            foreach (var operationHandle in _asyncOperationHandlesList.Where(operationHandle => operationHandle.IsValid())) {
                Addressables.Release(operationHandle);
            }

            _asyncOperationHandlesList.Clear();
        }

        #endregion
    }
}