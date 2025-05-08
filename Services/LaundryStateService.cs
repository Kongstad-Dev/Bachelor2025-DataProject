using System.Collections.Generic;
using System.Linq;
using BlazorTest.Database;
using BlazorTest.Models;
using BlazorTest.Services.Analytics;

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
        public List<SearchItem> BankIdItems { get; private set; } = new();

        public List<ChartDataPoint> RevenueOverTimeDataPoints { get; private set; } = new List<ChartDataPoint>();



        // State change event
        public event Action? OnStateChanged;
        public event Action? OnRevenueOverTimeDataPointsChanged;

        // Updates collections with external values
        public void UpdateBankItems(List<SearchItem> items)
        {
            BankItems = items;
            NotifyStateChanged();
        }

        public void UpdateBankIdItems(List<SearchItem> items)
        {
            BankIdItems = items;
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

        public void UpdateRevenueOverTimeDataPoints(List<ChartDataPoint> dataPoints)
        {
            RevenueOverTimeDataPoints = dataPoints;
            OnRevenueOverTimeDataPointsChanged?.Invoke();
            NotifyStateChanged();
        }

        public List<ChartDataPoint> GetRevenueOverTimeDataPoints() => RevenueOverTimeDataPoints;

        public List<Laundromat> GetEffectiveSelectedLaundromats()
        {
            return SelectedLaundromats;
        }

        public List<string> GetEffectiveSelectedLaundromatsIds()
        {
            return SelectedLaundromats.Select(l => l.kId).ToList();
        }
        public void RemoveSelectedLaundromat(Laundromat laundromat)
        {
                SelectedLaundromats.Remove(laundromat);
                NotifyStateChanged();
        }

        public void ClearSelectedLaundromatsAndBanks()
        {
            // Clear bank selections
            SelectedBanks.Clear();

            // Clear laundromat selections
            SelectedLaundromats.Clear();

            // Reset the laundromatItems to show all laundromats again
            if (LaundromatItemsOriginal.Any())
            {
                LaundromatItems = new List<SearchItem>(LaundromatItemsOriginal);

                // Also reset the ERP ID items to include all
                ErpIdItems = LaundromatItemsOriginal.Select(item =>
                {
                    var laundromat = item.Data as Laundromat;
                    return new SearchItem
                    {
                        Id = item.Id,
                        DisplayText = laundromat?.externalId ?? "No ERP ID",
                        Data = laundromat
                    };
                }).OrderBy(item => item.DisplayText).ToList();
            }

            NotifyStateChanged();
        }

        public DateTime? StartDate { get; private set; }
        public DateTime? EndDate { get; private set; }

        public void UpdateStartDate(DateTime? startDate)
        {
            StartDate = startDate;
            NotifyStateChanged();
        }

        public DateTime? GetStartDate()
        {
            return StartDate;
        }

        public DateTime? GetEndDate()
        {
            return EndDate;
        }

        public void UpdateEndDate(DateTime? endDate)
        {
            EndDate = endDate;
            NotifyStateChanged();
        }

        public async Task ResetLaundromatFilters()
        {
            // Clear bank selections
            SelectedBanks.Clear();

            // Clear laundromat selections
            SelectedLaundromats.Clear();

            // Reset the laundromatItems to show all laundromats again
            if (LaundromatItemsOriginal.Any())
            {
                LaundromatItems = new List<SearchItem>(LaundromatItemsOriginal);

                // Also reset the ERP ID items to include all
                ErpIdItems = LaundromatItemsOriginal.Select(item =>
                {
                    var laundromat = item.Data as Laundromat;
                    return new SearchItem
                    {
                        Id = item.Id,
                        DisplayText = laundromat?.externalId ?? "No ERP ID",
                        Data = laundromat
                    };
                }).OrderBy(item => item.DisplayText).ToList();
            }

            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();
    }


}
