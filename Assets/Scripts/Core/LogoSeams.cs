using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// The file box seam (#226): "ask the person for one picture file, then call back with its bytes, or with null on Cancel".
    /// The Editor has one version (its open-file box); the web and Windows versions come later (#228, #229). Tests use a fake.
    /// The callback may come at once or later (a browser file box answers after the page has gone on).
    /// </summary>
    public interface IPicturePicker
    {
        void Ask(Action<byte[]> done);
    }

    /// <summary>Picture files to pixels and back. Core has no Unity types, so the Game side supplies the image reader.</summary>
    public interface IPictureCodec
    {
        /// <summary>Reads a PNG or JPG file into RGBA bytes (row 0 at the top). False: not a picture.</summary>
        bool TryDecode(byte[] file, out int width, out int height, out byte[] rgba);

        byte[] EncodePng(int width, int height, byte[] rgba);
    }

    /// <summary>The saved pictures: one PNG per id in the save folder's BrandArt folder.</summary>
    public interface ILogoFiles
    {
        /// <summary>Writes the picture for id. False when it could not be written.</summary>
        bool Write(string id, byte[] png);

        void Delete(string id);

        bool Exists(string id);

        /// <summary>The saved PNG bytes, or null when there is none.</summary>
        byte[] Read(string id);
    }
}
