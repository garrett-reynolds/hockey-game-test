using System.IO;
using System.Linq;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Settings page's Brands tick boxes (#223): which pool brands stay in the rotation, kept in the settings file.</summary>
    public class BrandSettingsTests
    {
        Brands.Brand _current;
        System.Collections.Generic.IReadOnlyList<Brands.Brand> _ice;

        [SetUp]
        public void Keep() { _current = Brands.Current; _ice = Brands.Ice; }

        [TearDown]
        public void PutBack() { Brands.UseOff(""); Brands.Use(_current); Brands.UseIce(_ice); }

        static int Row(SettingsMenu menu, string label)
        {
            menu.ShowPart(SettingsPart.Brands); // 43 tick boxes: their own part (tab)
            for (int i = 0; i < menu.RowCount; i++)
                if (menu.Label(i) == label) return i;
            return -1;
        }

        [Test]
        public void Every_pool_brand_is_ticked_by_default_and_the_Leafs_are_not_in_the_list()
        {
            var save = new SaveData();
            foreach (Brands.Brand b in Brands.All) Assert.IsTrue(save.IsBrandOn(b.Id), b.Id);
            var menu = new SettingsMenu(save, null);
            foreach (Brands.Brand b in Brands.All)
            {
                int row = Row(menu, b.Name);
                Assert.GreaterOrEqual(row, 0, b.Name + " has a tick box");
                Assert.AreEqual("BRANDS", menu.Group(row));
                Assert.AreEqual("ON", menu.Value(row));
            }
            Assert.AreEqual(-1, Row(menu, "Toronto Maple Leafs"), "the centre-ice logo is not a choice");
        }

        [Test]
        public void Unticked_brands_leave_the_pool()
        {
            var off = string.Join(",", Brands.All.Skip(2).Select(b => b.Id));
            CollectionAssert.AreEqual(Brands.All.Take(2), Brands.Pool(off));
            CollectionAssert.AreEqual(Brands.All, Brands.Pool(""), "nothing off: the whole pool");
            CollectionAssert.AreEqual(Brands.All, Brands.Pool("not-a-brand"), "an unknown id changes nothing");
        }

        [Test]
        public void The_Rounds_and_the_ice_use_only_ticked_brands_and_the_ice_may_repeat_when_fewer_than_four()
        {
            Brands.Brand[] on = { Brands.All[3], Brands.All[7] };
            Brands.UseOff(string.Join(",", Brands.All.Except(on).Select(b => b.Id)));
            for (int i = 0; i < 20; i++)
            {
                Brands.NextRound();
                CollectionAssert.Contains(on, Brands.Current, "round " + i);
                Assert.AreEqual(4, Brands.Ice.Count);
                foreach (Brands.Brand b in Brands.Ice) CollectionAssert.Contains(on, b, "ice, round " + i);
            }
        }

        [Test]
        public void Turning_brands_off_changes_the_current_brand_if_it_was_turned_off()
        {
            Brands.Use(Brands.All[0]);
            Brands.UseOff(Brands.All[0].Id);
            Assert.AreNotSame(Brands.All[0], Brands.Current);
            foreach (Brands.Brand b in Brands.Ice) Assert.AreNotSame(Brands.All[0], b);
        }

        [Test]
        public void The_last_ticked_brand_cannot_be_unticked()
        {
            var save = new SaveData();
            foreach (Brands.Brand b in Brands.All.Skip(1)) Assert.IsTrue(save.SetBrandOn(b.Id, false), b.Id);
            Assert.IsFalse(save.SetBrandOn(Brands.All[0].Id, false), "the last one stays");
            Assert.IsTrue(save.IsBrandOn(Brands.All[0].Id));

            var menu = new SettingsMenu(save, null);
            int row = Row(menu, Brands.All[0].Name);
            menu.Select(row);
            Assert.IsFalse(menu.Activate(), "the page refuses too");
            Assert.AreEqual("ON", menu.Value(row));
        }

        [Test]
        public void A_tick_box_waits_for_Save_like_the_other_rows()
        {
            var save = new SaveData();
            var menu = new SettingsMenu(save, null);
            menu.Select(Row(menu, Brands.All[5].Name));
            Assert.IsTrue(menu.Activate());
            Assert.IsTrue(save.IsBrandOn(Brands.All[5].Id), "not before Save");
            Assert.IsTrue(menu.Save());
            Assert.IsFalse(save.IsBrandOn(Brands.All[5].Id), "after Save");
        }

        [Test]
        public void Unticked_brands_are_kept_in_the_settings_file()
        {
            string path = Path.Combine(Path.GetTempPath(), "hockey-t223-" + System.Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                var save = new SaveData();
                save.SetBrandOn(Brands.All[2].Id, false);
                save.SetBrandOn(Brands.All[9].Id, false);
                Assert.IsTrue(save.Save(path));
                var back = SaveData.Load(path);
                Assert.IsFalse(back.IsBrandOn(Brands.All[2].Id));
                Assert.IsFalse(back.IsBrandOn(Brands.All[9].Id));
                Assert.IsTrue(back.IsBrandOn(Brands.All[3].Id));
                Assert.AreEqual(save.BrandsOff, back.BrandsOff);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
