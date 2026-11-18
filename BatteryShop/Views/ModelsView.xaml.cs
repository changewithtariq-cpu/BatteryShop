using System.Windows;
using System.Windows.Controls;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Models;

namespace BatteryShop.Views;

public partial class ModelsView : UserControl
{
    private Brand _brand;          // brand being edited (null = new)
    private BatteryModel _model;   // model being edited (null = new)

    public ModelsView()
    {
        InitializeComponent();
        FType.ItemsSource = BatteryModel.Types;
        FType.SelectedIndex = 0;
        Loaded += (_, _) => LoadAll();
    }

    private void LoadAll()
    {
        var brands = CatalogRepo.GetBrands(activeOnly: false);
        BrandList.ItemsSource = brands;
        FBrand.ItemsSource = brands.Where(b => b.IsActive).ToList();
        ModelGrid.ItemsSource = CatalogRepo.GetModels(activeOnly: ShowInactive.IsChecked != true);
    }

    private void Reload(object sender, RoutedEventArgs e) => LoadAll();

    // ---------------------------------------------------------------- Brands
    private void BrandList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BrandList.SelectedItem is not Brand b) return;
        _brand = b;
        BrandName.Text = b.Name;
        BrandActive.IsChecked = b.IsActive;
    }

    private void NewBrand_Click(object sender, RoutedEventArgs e)
    {
        _brand = null;
        BrandList.SelectedItem = null;
        BrandName.Text = "";
        BrandActive.IsChecked = true;
        BrandName.Focus();
    }

    private void SaveBrand_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            var b = new Brand { Id = _brand?.Id ?? 0, Name = BrandName.Text, IsActive = BrandActive.IsChecked == true };
            CatalogRepo.SaveBrand(b);
            LoadAll();
            NewBrand_Click(null, null);
        });
    }

    // ---------------------------------------------------------------- Models
    private void ModelGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelGrid.SelectedItem is not BatteryModel m) return;
        _model = m;
        FormTitle.Text = "Edit model";
        FBrand.SelectedItem = ((IEnumerable<Brand>)FBrand.ItemsSource).FirstOrDefault(b => b.Id == m.BrandId);
        FModel.Text = m.ModelName;
        FAh.Text = m.CapacityAh.ToString();
        FVolt.Text = m.Voltage.ToString();
        FType.SelectedItem = m.BatteryType;
        FPrice.Text = m.DefaultSalePrice.ToString("N0");
        FWarranty.Text = m.WarrantyMonths.ToString();
        FWeight.Text = m.ApproxWeightKg?.ToString() ?? "";
        FLow.Text = m.LowStockLevel.ToString();
        FActive.IsChecked = m.IsActive;
    }

    private void NewModel_Click(object sender, RoutedEventArgs e)
    {
        _model = null;
        ModelGrid.SelectedItem = null;
        FormTitle.Text = "New model";
        FModel.Text = FAh.Text = FPrice.Text = FWeight.Text = "";
        FVolt.Text = "12";
        FWarranty.Text = "0";
        FLow.Text = "2";
        FType.SelectedIndex = 0;
        FActive.IsChecked = true;
        FModel.Focus();
    }

    private void SaveModel_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            var m = new BatteryModel
            {
                Id = _model?.Id ?? 0,
                BrandId = (FBrand.SelectedItem as Brand)?.Id ?? 0,
                ModelName = FModel.Text,
                CapacityAh = UI.Positive(FAh.Text, "Capacity (Ah)"),
                Voltage = UI.Positive(FVolt.Text, "Voltage"),
                BatteryType = FType.SelectedItem as string ?? "Other",
                DefaultSalePrice = UI.NonNegative(FPrice.Text, "Sale price"),
                WarrantyMonths = UI.NonNegative(FWarranty.Text, "Warranty (months)"),
                ApproxWeightKg = UI.Decimal(FWeight.Text, "Weight"),
                LowStockLevel = UI.NonNegative(FLow.Text, "Low stock alert"),
                IsActive = FActive.IsChecked == true
            };
            CatalogRepo.SaveModel(m);
            LoadAll();
            NewModel_Click(null, null);
        });
    }
}
