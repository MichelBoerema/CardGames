using UnityEngine;
using UnityEngine.UI;
using System.IO;

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

            Texture2D original = new Texture2D(2, 2);
            original.LoadImage(File.ReadAllBytes(path));

            Texture2D resized = Resize(original, 256, 256);

            // Encode + save locally
            byte[] pngBytes = resized.EncodeToPNG();
            string base64 = System.Convert.ToBase64String(pngBytes);

            PlayerPrefs.SetString(AVATAR_PREF_KEY, base64);
            PlayerPrefs.Save();

            // Show preview in lobby
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

        byte[] data = System.Convert.FromBase64String(
            PlayerPrefs.GetString(AVATAR_PREF_KEY)
        );

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
}
