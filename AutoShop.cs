﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using LeagueSharp;
using LeagueSharp.Common;

namespace AutoBuyerSharp
{
    public enum ItemCondition
    {
        TAKE_PRIMARY = 0,
        ENEMY_AP = 1,
        ENEMY_MR = 2,
        ENEMY_RANGED = 3,
        ENEMY_LOSING = 4,
    }

    public class AutoShop
    {
        private static readonly List<FullItem> itemList = new List<FullItem>();

        private static Obj_AI_Hero player = ObjectManager.Player;

        private static Build curBuild;

        private static bool gotStartingItems = false;

        private static bool willGoOverItem = false;

        private static List<InvItem> inv = new List<InvItem>();
        private static List<InvItem> inv2 = new List<InvItem>();

        private static List<int> canBuyOnfull = new List<int>();

        private static bool finished = false;

        public static void init()
        {
            string itemJson = "http://ddragon.leagueoflegends.com/cdn/6.12.1/data/en_US/item.json";
            string itemsData = Request(itemJson);
            string itemArray = itemsData.Split(new[] { "data" }, StringSplitOptions.None)[1];
            MatchCollection itemIdArray = Regex.Matches(itemArray, "[\"]\\d*[\"][:][{].*?(?=},\"\\d)");
            foreach (FullItem item in from object iItem in itemIdArray select new FullItem(iItem.ToString()))
                itemList.Add(item);
            Console.WriteLine("Finished build init");
            finished = true;
        }

        public static void setBuild(Build build)
        {
            curBuild = build;
        }
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        private static string Request(string url)
        {
            WebRequest request = WebRequest.Create(url);
            request.Credentials = CredentialCache.DefaultCredentials;
            WebResponse response = request.GetResponse();
            Stream dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream: dataStream);
            string responseFromServer = reader.ReadToEnd();
            reader.Close();
            response.Close();
            return responseFromServer;
        }

        public static void buyNext()
        {
            if (!finished)
                return;
            if (!gotStartingItems && player.Level < 4)
            {
                //Console.WriteLine("Buying starting items");
                foreach (var item in curBuild.startingItems)
                {
                    player.BuyItem(item);
                }
                gotStartingItems = true;
            }
            else
            {
                willGoOverItem = false;
                var best = getBestBuyItem();
                if (best == -1)
                    return;
                //if (willGoOverItem || !inventoryFull())
                player.BuyItem((ItemId)best);
                //  else
                // {
                //      Console.WriteLine("wont buy");
                // }
            }
        }

        private static int freeSlots()
        {
            return ObjectManager.Player.InventoryItems.Count(y => !y.IData.DisplayName.Contains("Poro")) - 2;
        }

        /// <summary>
        /// Check if inventory is full
        /// </summary>
        /// <returns></returns>
        private static bool inventoryFull()
        {
            return freeSlots() == 6;
        }

        /// <summary>
        /// Calculate the most appropriate item to purchase.
        /// </summary>
        /// <returns>Item ID</returns>
        private static int getBestBuyItem()
        {
            foreach (var item in curBuild.coreItems)
            {
                if (item.gotIt())
                    continue;

                List<BuyItem> chain = new List<BuyItem>();
                fillItemsChain(item.getBest().Id, ref chain, true);
                //Console.WriteLine("chain: " + chain.Count);

                var bestItem =
                    chain.Where(sel => sel.price <= player.Gold && (!inventoryFull() || canBuyOnfull.Contains(sel.item.Id))).OrderByDescending(sel2 => sel2.price).FirstOrDefault();
                if (bestItem == null || bestItem.price == 0)
                    return -1;
                //Console.WriteLine("Buy: " + bestItem.item.Name);
                return bestItem.item.Id;
            }
            return -1;
        }

        private static void fillItemsChain(int id, ref List<BuyItem> ids, bool start = false, int masterId = -1)
        {
            if (start)
            {
                canBuyOnfull.Clear();
                inv2.Clear();
                inv2 = player.InventoryItems.Select(iIt => new InvItem
                {
                    id = (int)iIt.Id
                }).ToList();
            }

            foreach (var itNow in inv2.Where(it => !it.used()))
            {
                if (itNow.id == id)
                {
                    canBuyOnfull.Add(masterId);
                    willGoOverItem = true;
                    itNow.setUsed();
                    return;
                }
            }

            var data = getData(id);

            var test = new BuyItem
            {
                item = data,
                price = getItemsPrice(id, true)
            };
            //Console.WriteLine("item: "+test.item.Name+" : price "+test.price);
            ids.Add(test);
            if (data.From != null)
                foreach (var item in data.From)
                {
                    //Console.WriteLine("req Item: "+(int)item);
                    fillItemsChain(item, ref ids, false, id);
                }
        }

        private static int getItemsPrice(int item, bool start = false)
        {
            if (start)
            {
                inv.Clear();
                inv = player.InventoryItems.Select(iIt => new InvItem
                {
                    id = (int)iIt.Id
                }).ToList();
            }

            foreach (var itNow in inv.Where(it => !it.used()))
            {
                if (itNow.id == item)
                {
                    willGoOverItem = true;
                    itNow.setUsed();
                    return 0;
                }
            }

            var FullItem = getData(item);
            return FullItem.Goldbase + ((FullItem.From != null) ? FullItem.From.Sum(req => getItemsPrice(req)) : 0);
        }

        public static FullItem getData(int id)
        {
            //Console.WriteLine("trying to get data: " + id);
            var item = itemList.FirstOrDefault(it => it.Id == id);
            //if(item == null)
            //Console.WriteLine("cant get data " + id);
            return item;
        }

        private static int itemCount(int id, Obj_AI_Hero hero = null)
        {
            return (hero ?? ObjectManager.Player).InventoryItems.Count(slot => (int)slot.Id == id);
        }

        public static bool gotItem(int id, Obj_AI_Hero hero = null)
        {
            return (hero ?? ObjectManager.Player).InventoryItems.Any(slot => (int)slot.Id == id);
        }

    }

    public class Build
    {
        public List<ItemId> startingItems;
        public List<ConditionalItem> coreItems;
    }

    public class BuyItem
    {
        public FullItem item;
        public int price;
    }


    public class InvItem
    {
        private bool got = false;
        public int id;

        public bool used()
        {
            return got;
        }

        public void setUsed()
        {
            got = true;
        }
    }

    public class ConditionalItem
    {

        private FullItem selected;

        private FullItem primary;
        private FullItem secondary;
        private ItemCondition condition;

        public ConditionalItem(int pri, int sec = -1, ItemCondition cond = ItemCondition.TAKE_PRIMARY)
        {
            primary = AutoShop.getData(pri);
            secondary = (sec == -1) ? null : AutoShop.getData(sec);
            condition = cond;
        }

        public ConditionalItem(ItemId pri, ItemId sec = ItemId.Unknown, ItemCondition cond = ItemCondition.TAKE_PRIMARY)
        {
            primary = AutoShop.getData((int)pri);
            secondary = (sec == ItemId.Unknown) ? null : AutoShop.getData((int)sec);
            condition = cond;
        }

        public bool gotIt()
        {
            return primary == null || AutoShop.gotItem(primary.Id) || (secondary != null && AutoShop.gotItem(secondary.Id));
        }

        public FullItem getBest()
        {
            if (selected != null)
                return selected;
            if (condition == ItemCondition.TAKE_PRIMARY)
                selected = primary;
            if (condition == ItemCondition.ENEMY_MR)
            {
                if (HeroManager.Enemies.Sum(ene => ene.SpellBlock) > HeroManager.Enemies.Sum(ene => ene.Armor))
                    selected = primary;
                else
                    selected = secondary;
            }

            if (condition == ItemCondition.ENEMY_RANGED)
            {
                if (HeroManager.Enemies.Count(ene => ene.IsRanged) > HeroManager.Enemies.Count(ene => ene.IsMelee))
                    selected = primary;
                else
                    selected = secondary;
            }

            if (condition == ItemCondition.ENEMY_AP)
            {
                if (HeroManager.Enemies.Sum(ene => ene.FlatMagicDamageMod) > HeroManager.Enemies.Sum(ene => ene.FlatPhysicalDamageMod) * 2.0f)
                    selected = primary;
                else
                    selected = secondary;
            }
            if (condition == ItemCondition.ENEMY_LOSING)
            {
                var allTowers = ObjectManager.Get<Obj_AI_Turret>().ToList();
                if (allTowers.Count(tow => tow.IsDead && tow.IsEnemy) > allTowers.Count(tow => tow.IsDead && tow.IsAlly))
                    selected = primary;
                else
                    selected = secondary;
            }


            return selected;
        }
    }
}
