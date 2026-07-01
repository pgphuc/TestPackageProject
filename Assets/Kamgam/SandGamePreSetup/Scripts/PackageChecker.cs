#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace Kamgam.SandGame.PreSetup
{
    public class PackageChecker
    {
        static ListRequest _request;
        static System.Action<ListRequest> _onComplete;

        public static void LoadPackagesInfos(System.Action<ListRequest> onComplete)
        {
            _onComplete = onComplete;
            _request = Client.List(offlineMode: true);
            EditorApplication.update += progress;
        }

        static void progress()
        {
            if (_request.IsCompleted)
            {
                _onComplete?.Invoke(_request);
                EditorApplication.update -= progress;
            }
        }

        public static bool ContainsPackage(ListRequest request, string packageId)
        {
            if (request.Status != StatusCode.Success)
                return false;

            return ContainsPackage(request.Result, packageId);
        }

        public static bool ContainsPackage(PackageCollection packages, string packageId)
        {
            foreach (var package in packages)
            {
                if (string.Compare(package.name, packageId) == 0)
                    return true;

                foreach (var dependencyInfo in package.resolvedDependencies)
                    if (string.Compare(dependencyInfo.name, packageId) == 0)
                        return true;
            }

            return false;
        }
    }
}
#endif