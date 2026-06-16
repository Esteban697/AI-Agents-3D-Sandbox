using UnityEngine;
using System;
using System.Collections;

namespace Agent_Mechanics
{
    /// <summary>
    /// Mono-behavior that handles how to capture and copy the texture from camera to be useful as image for model
    /// </summary>
    public class CameraFrameGetter : MonoBehaviour
    {
        [Header("Camera to capture image")] [SerializeField]
        private Camera sourceCamera;

        public IEnumerator GetTextureFromCamera(int targetWidth, int targetHeight, Action<Texture2D> callback)
        {
            RenderTexture renderTexture = new RenderTexture(targetWidth, targetHeight, 24);
            renderTexture.antiAliasing = 1;
            Texture2D screenShot = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);

            sourceCamera.targetTexture = renderTexture;
            sourceCamera.Render();

            yield return new WaitForEndOfFrame();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = renderTexture;

            screenShot.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            screenShot.Apply(false, false);

            RenderTexture.active = prev;
            sourceCamera.targetTexture = null;
            renderTexture.Release();
            callback(screenShot);
        }

        public void CopyCapturedTexture(Texture2D inTex, out Texture2D outTex, bool considerMipmapLimits)
        {
            int width = inTex.width;
            int height = inTex.width;

            Texture2DArray outArray = new Texture2DArray(width, height, 24, inTex.format, false);
            outTex = new Texture2D(width, height, inTex.format, false);

            if (!considerMipmapLimits)
            {
                // Texture2D -> Texture2DArray
                // Global Mipmap Limit: "1: Half Resolution" => copies into mips that are now too large; will copy each mip of inTex into a quarter of outArray
                for (int mip = 0; mip < inTex.mipmapCount; ++mip)
                {
                    int copyWidth = width >> mip;
                    int copyHeight = height >> mip;
                    Graphics.CopyTexture(inTex, 0, mip, 0, 0, copyWidth, copyHeight, outArray, 0, mip, 0, 0);
                }

                // Texture2DArray -> Texture2D
                // Global Mipmap Limit: "1: Half Resolution" => errors, since we try to copy into mips that are now too small
                for (int mip = 0; mip < outArray.mipmapCount; ++mip)
                {
                    int copyWidth = width >> mip;
                    int copyHeight = height >> mip;
                    Graphics.CopyTexture(outArray, 0, mip, 0, 0, copyWidth, copyHeight, outTex, 0, mip, 0, 0);
                }
            }
            else // considering mipmap limits
            {
                int globalMipmapLimit = QualitySettings.globalTextureMipmapLimit;

                // Texture2D -> Texture2DArray
                // Global Mipmap Limit: "1: Half Resolution" => mip0 of outArray is not written to, other mips copy as expected
                //  (ALTERNATIVE: if outArray creation already considered globalMipmapLimit for its dimensions,
                //   the CopyTexture call can ignore globalMipmapLimit since the mips will line up again)
                for (int mip = 0; mip < inTex.mipmapCount - globalMipmapLimit; ++mip)
                {
                    int copyWidth = width >> mip;
                    int copyHeight = height >> mip;
                    int srcMip = mip;
                    int dstMip = mip + globalMipmapLimit;
                    Graphics.CopyTexture(inTex, 0, srcMip, 0, 0, copyWidth, copyHeight, outArray, 0, dstMip, 0, 0);
                }

                // Texture2DArray -> Texture2D
                // Global Mipmap Limit: "1: Half Resolution" => mip0 of outArray is not copied (but outTex does not upload it to GPU anyway)
                for (int mip = globalMipmapLimit; mip < outArray.mipmapCount; ++mip)
                {
                    int copyWidth = width >> mip;
                    int copyHeight = height >> mip;
                    int srcMip = mip;
                    int dstMip = mip - globalMipmapLimit;
                    Graphics.CopyTexture(outArray, 0, srcMip, 0, 0, copyWidth, copyHeight, outTex, 0, dstMip, 0, 0);
                }
            }
        }
    }
}
