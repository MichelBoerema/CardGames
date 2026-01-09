using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

public class LobbyAvatarController : MonoBehaviour
{
    [Header("UI")]
    public Image avatarPreview;

    private const string AVATAR_PREF_KEY = "PlayerAvatar";

    public void TakePhoto()
    {
        NativeCamera.TakePicture((path) =>
        {
            if (string.IsNullOrEmpty(path))
                return;

            // Load the image with correct orientation
            Texture2D original = NativeCamera.LoadImageAtPath(path, 512, false, false);
            // Parameters: path, maxSize, markNonReadable=false, generateMipmaps=false
            // By default, LoadImageAtPath auto-applies EXIF orientation

            if (original == null)
                return;

            // Resize if needed
            Texture2D resized = Resize(original, 256, 256);

            // Encode + save locally
            byte[] pngBytes = resized.EncodeToPNG();
            string base64 = System.Convert.ToBase64String(pngBytes);

            PlayerPrefs.SetString(AVATAR_PREF_KEY, base64);
            PlayerPrefs.Save();

            // Show preview
            avatarPreview.sprite = Sprite.Create(
                resized,
                new Rect(0, 0, resized.width, resized.height),
                new Vector2(0.5f, 0.5f)
            );
            avatarPreview.enabled = true;

        }, maxSize: 512);

    }

    public static Texture2D LoadSavedAvatar()
    {
        if (!PlayerPrefs.HasKey(AVATAR_PREF_KEY))
            return null;

        byte[] data = Convert.FromBase64String(PlayerPrefs.GetString(AVATAR_PREF_KEY));

        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(data);
        return tex;
    }

    private Texture2D Resize(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    private Texture2D CorrectOrientation(string path, Texture2D tex)
    {
#if UNITY_IOS || UNITY_ANDROID
        try
        {
            var exif = NativeCamera.GetImageProperties(path); // width, height, orientation
            switch (exif.orientation)
            {
                case NativeCamera.ImageOrientation.Normal:
                    // No rotation needed
                    break;
                case NativeCamera.ImageOrientation.Rotate90:
                    tex = RotateTexture(tex, true);  // clockwise 90°
                    break;
                case NativeCamera.ImageOrientation.Rotate180:
                    tex = RotateTexture180(tex);     // upside-down
                    break;
                case NativeCamera.ImageOrientation.Rotate270:
                    tex = RotateTexture(tex, false); // counter-clockwise 90°
                    break;
                case NativeCamera.ImageOrientation.FlipHorizontal:
                    tex = FlipTexture(tex, true);    // horizontal flip
                    break;
                case NativeCamera.ImageOrientation.FlipVertical:
                    tex = FlipTexture(tex, false);   // vertical flip
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Failed to read EXIF orientation: " + e);
        }
#endif
        return tex;
    }

    // Clockwise if true, counter-clockwise if false
    private Texture2D RotateTexture(Texture2D original, bool clockwise)
    {
        int w = original.width;
        int h = original.height;
        Texture2D rotated = new Texture2D(h, w);

        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                if (clockwise)
                    rotated.SetPixel(h - j - 1, i, original.GetPixel(i, j));
                else
                    rotated.SetPixel(j, w - i - 1, original.GetPixel(i, j));
            }
        }
        rotated.Apply();
        return rotated;
    }

    private Texture2D RotateTexture180(Texture2D original)
    {
        int w = original.width;
        int h = original.height;
        Texture2D rotated = new Texture2D(w, h);

        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                rotated.SetPixel(w - i - 1, h - j - 1, original.GetPixel(i, j));
            }
        }
        rotated.Apply();
        return rotated;
    }

    private Texture2D FlipTexture(Texture2D original, bool horizontal)
    {
        int w = original.width;
        int h = original.height;
        Texture2D flipped = new Texture2D(w, h);

        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                if (horizontal)
                    flipped.SetPixel(w - i - 1, j, original.GetPixel(i, j));
                else
                    flipped.SetPixel(i, h - j - 1, original.GetPixel(i, j));
            }
        }
        flipped.Apply();
        return flipped;
    }

}
