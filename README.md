# Ayaz Battery & Solar Service – Shop Management (Phase 1)

Offline Windows desktop app: C# WPF, .NET 8, SQLite.

## How to run

1. Install **Visual Studio 2022** with the **.NET desktop development** workload.
2. Open `BatteryShop.sln`.
3. Press **F5**. On first build Visual Studio downloads one package (`Microsoft.Data.Sqlite`), so the
   first build needs internet. After that, the app runs fully offline.

The database is created automatically on first start:
`%LOCALAPPDATA%\AyazBatteryShop\shop.db`

Daily automatic backups go to `Documents\AyazBatteryShop Backups` (change it in Settings).

## First-time setup (in the app)

1. **Settings & Backup** – check the shop details, pick the 80mm receipt printer, click *Print test receipt*.
2. **Battery Models** – add brands, then models (Ah, sale price, warranty months).
3. **Suppliers** – add your dealers.
4. **Stock** – *Add opening / second-hand stock* for batteries already in the shop.
5. **Purchases** – new stock from dealers (serial numbers optional but recommended).
6. **New Sale (F2)** – scan / type serial or pick a battery, add old-battery exchange, enter paid amount, *Save & Print (F9)*.

## Keyboard shortcuts

| Key | Screen |
|-----|--------|
| F1 | Dashboard |
| F2 | New Sale |
| F3 | Sales History |
| F4 | Customers |
| F5 | Stock |
| F6 | Purchases |
| F9 | Save & Print (on New Sale) |

## Project layout

```
BatteryShop/
  App.xaml              start-up, styles, colours
  MainWindow.xaml       sidebar + page area
  Data/                 Schema.sql (database tables), Db.cs (helpers), *Repo.cs (all database work)
  Models/               plain data classes
  Views/                screens (each .xaml + .xaml.cs)
  Printing/             80mm receipt layout and printing
  Services/             backup / restore
  Helpers/              messages, number parsing
```

## Rules built into the app

- Money is stored in whole rupees.
- Stock and balances are calculated from records, never typed in.
- Nothing is deleted: sales, purchases and payments are **cancelled** (voided) with a reason.
- Walk-in customer must pay in full; credit / partial sales need a named customer.
- Old batteries taken in exchange are saved as *Pending*; Phase 2 will move them to scrap / second-hand stock.
