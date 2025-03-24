using System.Collections.Generic;
using System.Linq;
using BlazorTest.Database;
using BlazorTest.Models;

namespace BlazorTest.Services
{
    public class LaundryStateService
    {
        // Bank state
        public List<SearchItem> BankItems { get; private set; } = new List<SearchItem>();
        public List<BankEntity> SelectedBanks { get; private set; } = new List<BankEntity>();

        // Laundromat state
        public List<SearchItem> LaundromatItems { get; private set; } = new List<SearchItem>();
        public List<Laundromat> SelectedLaundromats { get; private set; } = new List<Laundromat>();
        public List<SearchItem> LaundromatItemsOriginal { get; private set; } =
            new List<SearchItem>();
        public List<SearchItem> ErpIdItems { get; private set; } = new List<SearchItem>();

        // State change event
        public event Action OnStateChanged;

        // Updates collections with external values
        public void UpdateBankItems(List<SearchItem> items)
        {
            BankItems = items;
            NotifyStateChanged();
        }

        public void UpdateSelectedBanks(List<BankEntity> banks)
        {
            SelectedBanks = banks;
            NotifyStateChanged();
        }

        public void UpdateLaundromatItems(List<SearchItem> items)
        {
            LaundromatItems = items;
            NotifyStateChanged();
        }

        public void UpdateSelectedLaundromats(List<Laundromat> laundromats)
        {
            SelectedLaundromats = laundromats;
            NotifyStateChanged();
        }

        public void UpdateOriginalLaundromats(List<SearchItem> items)
        {
            LaundromatItemsOriginal = items;
            NotifyStateChanged();
        }

        public void UpdateErpIdItems(List<SearchItem> items)
        {
            ErpIdItems = items;
            NotifyStateChanged();
        }

        public List<Laundromat> GetEffectiveSelectedLaundromats()
        {
            // If user has made explicit selections, use those
            if (SelectedLaundromats.Any())
            {
                return SelectedLaundromats;
            }

            // Otherwise use ALL available laundromats as selected
            return LaundromatItems
                .Select(item => item.Data as Laundromat)
                .Where(l => l != null)
                .ToList();
        }

        public void RemoveSelectedLaundromat(Laundromat laundromat)
        {
            if (SelectedLaundromats.Count == 0)
            {
                SelectedLaundromats = LaundromatItems
                    .Select(item => item.Data as Laundromat)
                    .Where(l => l != null && l.kId != laundromat.kId)
                    .ToList();

                Console.WriteLine($"Selected all ({SelectedLaundromats.Count}) except one");
                NotifyStateChanged();
            }
            else
            {
                SelectedLaundromats.Remove(laundromat);
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();
    }
}
