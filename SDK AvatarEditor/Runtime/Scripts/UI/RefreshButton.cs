using System;
using Cysharp.Threading.Tasks;
using Genies.Customization.Framework;
using Genies.Inventory;
using Genies.ServiceManagement;
using Genies.UIFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Genies.AvatarEditor
{
    [RequireComponent(typeof(GeniesButton))]
    public class RefreshButton : MonoBehaviour
    {
        [SerializeField] private GeniesButton _refreshButton;
        [SerializeField] private Customizer _customizer;

        private void Awake()
        {
            if (_refreshButton == null)
            {
                _refreshButton = GetComponent<GeniesButton>();
            }

            if (_refreshButton != null)
            {
                _refreshButton.onClick.AddListener(Refresh);
            }

        }

        private void OnDestroy()
        {
            if (_refreshButton != null)
            {
                _refreshButton.onClick.RemoveAllListeners();
            }
        }

        public void Refresh()
        {
            RefreshAsync().Forget();
        }

        private async UniTaskVoid RefreshAsync()
        {
            // Clear the inventory caches so data is re-fetched from the server
            var defaultInventoryService = ServiceManager.Get<IDefaultInventoryService>();

            if (defaultInventoryService != null)
            {
                defaultInventoryService.ClearDefaultWearablesCache();
                defaultInventoryService.ClearUserWearablesCache();
            }

            // Refresh the current item picker view with the new data
            if (_customizer != null)
            {
                await _customizer.RefreshCurrentNodeDataAsync();
            }
        }
    }
}
