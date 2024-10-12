using Life;
using Life.BizSystem;
using Life.Network;
using Life.UI;
using ModKit.Helper;
using ModKit.Helper.ItemHelper.Classes;
using ModKit.Interfaces;
using ModKit.Internal;
using ModKit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using _menu = AAMenu.Menu;
using mk = ModKit.Helper.TextFormattingHelper;

namespace BetterItem
{
    public class BetterItem : ModKit.ModKit
    {
        public BetterItem(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Aarnow");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            InsertMenu();
            Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }

        public void InsertMenu()
        {
            _menu.AddAdminPluginTabLine(PluginInformations, 5, "BetterItem", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                BetterItemPanel(player);
            });
        }

        public async void BetterItemPanel(Player player)
        {
            var items = await ItemHelper.GetItems();
            var categories = await Category.QueryAll();

            Panel panel = PanelHelper.Create("BetterItem - Liste des objets", UIPanel.PanelType.TabPrice, player, () => BetterItemPanel(player));

            if (items != null && items.Count > 0)
            {
                foreach (var item in items)
                {
                    string categoryName = categories.Where(c => c.Id == item.CategoryId).FirstOrDefault().Name;
                    panel.AddTabLine($"{item.Name}<br>{categoryName}", $"{item.Price}€", ItemUtils.GetIconIdByItemId(item.ItemId), ui =>
                    {
                        ItemDetails(player, item);
                    });
                }
                panel.NextButton("Modifier", () => panel.SelectTab());
            }
            else
            {
                panel.AddTabLine("Aucun objet", _ => { });

                panel.NextButton("Importer", async () =>
                {
                    player.Notify("ModKit", "Nous importons les objets. Veuillez patienter un instant.", NotificationManager.Type.Info);

                    var shops = UnityEngine.Object.FindObjectsOfType<Life.CheckpointSystem.ShopCheckpoint>();

                    foreach (var shop in shops)
                    {
                        foreach (var cat in shop.Categories)
                        {
                            Category category = new Category();
                            category.Name = cat;
                            await category.Save();
                        }

                        List<Category> currentCategories = await Category.QueryAll();

                        foreach (var def in shop.Items)
                        {
                            Item newItem = new Item();
                            newItem.Name = def.item.itemName;
                            newItem.ItemId = def.item.id;
                            newItem.Price = def.price;
                            newItem.CategoryId = currentCategories.Where(c => c.Name == shop.Categories[def.categoryId]).First().Id;
                            await newItem.Save();
                        }
                    }

                    BetterItemPanel(player);
                });
            }

            panel.AddButton("Retour", ui => AAMenu.AAMenu.menu.AdminPluginPanel(player));
            panel.CloseButton();

            panel.Display();
        }

        public async void ItemDetails(Player player, Item item)
        {
            var categories = await Category.Query(c => c.Id == item.CategoryId);
            var categoryName = categories.FirstOrDefault()?.Name ?? "inconnu";

            Panel panel = PanelHelper.Create("BetterItem - Détails d'un objet", UIPanel.PanelType.TabPrice, player, () => ItemDetails(player, item));

            panel.AddTabLine($"{mk.Color("Nom:", mk.Colors.Info)} {item.Name}", "", ItemUtils.GetIconIdByItemId(item.ItemId), _ => SetItemName(player , item));
            panel.AddTabLine($"{mk.Color("Prix:", mk.Colors.Info)} {item.Price}", _ => SetItemPrice(player, item));
            panel.AddTabLine($"{mk.Color("Catégorie:", mk.Colors.Info)} {categoryName}", _ => SetItemCategory(player, item));
            panel.NextButton("Modifier", () => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        #region SETTERS
        public void SetItemName(Player player, Item item)
        {
            Panel panel = PanelHelper.Create("BetterItem - Modifier le nom de l'objet", UIPanel.PanelType.Input, player, () => SetItemName(player, item));

            panel.TextLines.Add("Donner un nom à cette objet");

            panel.PreviousButtonWithAction("Sélectionner", async () =>
            {
                if (panel.inputText != null && panel.inputText.Length >= 3)
                {
                    item.Name = panel.inputText;
                    if (await item.Save())
                    {
                        player.Notify("BetterItem", "Modification enregistrée", NotificationManager.Type.Success);
                        return true;
                    }
                    else player.Notify("BetterItem", "Nous n'avons pas pu enregistrer cette modification", NotificationManager.Type.Error);
                }
                else player.Notify("BetterItem", "3 caractères minimum", NotificationManager.Type.Warning);

                return false;
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        public void SetItemPrice(Player player, Item item)
        {
            Panel panel = PanelHelper.Create("BetterItem - Modifier le prix", UIPanel.PanelType.Input, player, () => SetItemPrice(player, item));

            panel.TextLines.Add("Donner un prix à cette objet");
            panel.inputPlaceholder = "exemple: 2500.35";

            panel.PreviousButtonWithAction("Sélectionner", async () =>
            {
                if (double.TryParse(panel.inputText, out double price))
                {
                    if (price > 0)
                    {
                        item.Price = Math.Round(price, 2);
                        if (await item.Save())
                        {
                            player.Notify("BetterItem", "Modification enregistrée", NotificationManager.Type.Success);
                            return true;
                        }
                        else player.Notify("BetterItem", "Nous n'avons pas pu enregistrer cette modification", NotificationManager.Type.Error);
                    }
                    else player.Notify("BetterItem", "Prix invalide (1€ minimum)", NotificationManager.Type.Warning);
                }
                else player.Notify("BetterItem", "Format incorrect", NotificationManager.Type.Warning);

                return false;
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        public async void SetItemCategory(Player player, Item item)
        {
            //Query
            var categories = await Category.QueryAll();

            Panel panel = PanelHelper.Create("BetterItem - Modifier la catégorie de l'objet", UIPanel.PanelType.TabPrice, player, () => SetItemCategory(player, item));

            foreach (var cat in categories)
            {
                panel.AddTabLine($"{mk.Color($"{cat.Name}",(item.CategoryId == cat.Id ? mk.Colors.Success : mk.Colors.Verbose))}", async _ =>
                {
                    item.CategoryId = cat.Id;
                    if (await item.Save())
                    {
                        player.Notify("BetterItem", "Modification enregistrée", NotificationManager.Type.Success);
                        panel.Previous();
                    }
                    else
                    {
                        player.Notify("BetterItem", "Nous n'avons pas pu enregistrer cette modification", NotificationManager.Type.Error);
                        panel.Refresh();
                    }
                });
            }

            panel.AddButton("Sélectionner", _ => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        #endregion
    }
}
