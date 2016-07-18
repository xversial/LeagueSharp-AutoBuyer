using System;
using LeagueSharp;
using LeagueSharp.Common;

namespace AutoBuyerSharp
{
    class ChampBuildManager
    {
        public static int now
        {
            get { return (int)DateTime.Now.TimeOfDay.TotalMilliseconds; }
        }

        public ChampBuildManager()
        {
            Console.WriteLine("AutoBuyer started!");
            CustomEvents.Game.OnGameLoad += onLoad;
        }

        public static Menu Config;
        public static int gameStart = 0;
        private void onLoad(EventArgs args)
        {
            gameStart = now;
            Game.PrintChat("LeagueSharp AutoBuyer by Xversial");
            try
            {
                Config = new Menu("AutoBuyer", "AutoBuyer", true);

                //Extra
                Config.AddSubMenu(new Menu("Extra Sharp", "extra"));
                Config.SubMenu("extra").AddItem(new MenuItem("buyItems", "Buy Items")).SetValue(true);
                Config.AddToMainMenu();

                //Drawing.OnDraw += onDraw;
                Game.OnUpdate += OnGameUpdate;

                //Setup shop
                AutoShop.init();
                AutoShop.setBuild(BuildManager.getBestBuild());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static int lastTick = now;

        private void OnGameUpdate(EventArgs args)
        {
            //dont try to buy every tick and some delay after game start
            if (lastTick + 888 > now || gameStart + 4090 > now)
                return;
            lastTick = now;

            if (!Config.Item("buyItems").GetValue<bool>())
                return;
            try
            {
                AutoShop.buyNext();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void onDraw(EventArgs args)
        {
        }

    }
}