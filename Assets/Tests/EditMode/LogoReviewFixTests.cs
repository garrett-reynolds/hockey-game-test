using System.Collections.Generic;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Tests
{
    /// <summary>#224 review fixes: made textures are destroyed when safe, the centre-ice picture is used as picked, the start-screen logo redraws only for the GAME LOGO.</summary>
    public class LogoReviewFixTests
    {
        sealed class FakeFiles : ILogoFiles
        {
            public readonly Dictionary<string, byte[]> Saved = new Dictionary<string, byte[]>();
            public bool Write(string id, byte[] png) { Saved[id] = png; return true; }
            public void Delete(string id) => Saved.Remove(id);
            public bool Exists(string id) => Saved.ContainsKey(id);
            public byte[] Read(string id) => Saved.TryGetValue(id, out var b) ? b : null;
        }

        const string Id = "t224-test";

        ILogoFiles _oldFiles;
        System.Func<string, bool> _oldIsCustom;
        FakeFiles _files;

        [SetUp]
        public void Open()
        {
            _oldFiles = CustomLogos.Files;
            _oldIsCustom = CustomLogos.IsCustom;
            CustomLogos.IsCustom = id => id == Id || id == LogoArt.CentreIceId; // never the real save file
            _files = new FakeFiles();
            CustomLogos.Files = _files;
            RgbaPicture logo = Core.Tests.LogoArtTests.LogoOnWhite(300, 200, 120, 50);
            byte[] png = CustomLogos.Codec.EncodePng(logo.Width, logo.Height, logo.Rgba);
            foreach (string id in new[] { Id, LogoArt.CentreIceId }) _files.Saved[id] = png;
        }

        [TearDown]
        public void Close()
        {
            foreach (string id in new[] { Id, LogoArt.CentreIceId })
            {
                CustomLogos.Forget(id);
                ArtLookup.Clear(id);
            }
            CustomLogos.ReleaseRetired(now: true);
            CustomLogos.Files = _oldFiles;
            CustomLogos.IsCustom = _oldIsCustom;
        }

        [Test]
        public void Saving_a_logo_again_and_again_does_not_grow_the_made_textures()
        {
            int before = CustomLogos.LiveTextures;
            Texture2D first = null;
            for (int i = 0; i < 3; i++)
            {
                Texture2D t = CustomLogos.Spot(Id, LogoSpots.Panel);
                Assert.IsNotNull(t);
                if (i == 0) first = t;
                CustomLogos.Forget(Id); // what SAVE does
                Assert.IsTrue(first != null || i > 0, "a forgotten texture is not destroyed at once: a Round in play may show it");
                CustomLogos.ReleaseRetired(now: true);
            }
            Assert.IsTrue(first == null, "the first made texture was destroyed");
            Assert.AreEqual(before, CustomLogos.LiveTextures);
        }

        [Test]
        public void A_forgotten_texture_lives_until_the_next_release_frame()
        {
            Texture2D t =CustomLogos.Spot(Id, LogoSpots.Panel);
            CustomLogos.Forget(Id);
            CustomLogos.ReleaseRetired(); // the same frame: nothing is destroyed
            Assert.IsTrue(t != null, "kept for the frame the new art is taken");
            CustomLogos.ReleaseRetired(now: true);
            Assert.IsTrue(t == null);
        }

        [Test]
        public void Centre_ice_uses_the_picked_picture_clear_round_it_not_the_ring_disc()
        {
            ArtLookup.Clear(LogoArt.CentreIceId);
            Texture2D art = ArtLookup.CentreIce();
            Assert.IsNotNull(art);
            Assert.AreEqual(LogoArt.CentreSize, art.width);
            Assert.AreEqual(LogoArt.CentreSize, art.height); // a square: the pixels are checked in LogoArtTests (made textures are not readable)
            StringAssert.Contains("custom-" + LogoArt.CentreIceId + "-" + LogoSpots.CentrePicked, art.name);
        }

        [Test]
        public void The_start_screen_logo_redraws_only_when_the_GAME_LOGO_picture_changed()
        {
            int game = ArtLookup.GameLogoVersion, all = ArtLookup.Version;
            ArtLookup.Clear("any-brand");
            ArtLookup.Clear(LogoArt.CentreIceId);
            Assert.AreEqual(game, ArtLookup.GameLogoVersion);
            Assert.AreEqual(all + 2, ArtLookup.Version);
            ArtLookup.Clear(LogoArt.GameLogoId);
            Assert.AreEqual(game + 1, ArtLookup.GameLogoVersion);
        }

        [Test]
        public void The_spot_names_are_one_list()
        {
            CollectionAssert.AllItemsAreUnique(LogoSpots.All);
            CollectionAssert.Contains(LogoSpots.All, LogoSpots.CentrePicked);
            CollectionAssert.Contains(LogoSpots.All, LogoSpots.GameLogo);
        }
    }
}
