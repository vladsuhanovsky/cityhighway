using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityHighways.Presentation
{
    /// <summary>Small independent city portrait window. Never changes the board camera,
    /// its projection, game input or global rendering settings.</summary>
    public sealed partial class City3DScene
    {
        Camera _characterCloseupCamera;
        RenderTexture _characterCloseupTexture;

        public static bool TryCharacterCloseupPose(Vector3 feet, float yaw, float zoom,
            out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (!CharacterObservation.IsFinite(feet) || float.IsNaN(yaw) || float.IsInfinity(yaw) ||
                float.IsNaN(zoom) || float.IsInfinity(zoom)) return false;
            var target = feet + Vector3.up * (CityProfileWalker.Height * .55f);
            float distance = .042f / Mathf.Clamp(zoom, .8f, 1.4f);
            var offset = Quaternion.Euler(0f, Mathf.Repeat(yaw, 360f), 0f) *
                new Vector3(0f, distance * .25f, distance);
            position = target + offset;
            rotation = Quaternion.LookRotation(-offset, Vector3.up);
            return CharacterObservation.IsFinite(position);
        }

        public bool TryShowCharacterCloseup(float yaw, float zoom, out RenderTexture texture)
        {
            texture = null;
            if (_root == null || _camera == null || !Application.isPlaying || Ui.ReducedMotion ||
                !TryGetProfilePedestrianPosition(out var feet) ||
                !TryCharacterCloseupPose(feet, yaw, zoom, out var position, out var rotation))
            { CloseCharacterCloseup(); return false; }
            try
            {
                if (_characterCloseupCamera == null || _characterCloseupTexture == null)
                {
                    CloseCharacterCloseup();
                    _characterCloseupTexture = new RenderTexture(384, 256, 24, RenderTextureFormat.ARGB32)
                    { name = "City character closeup", hideFlags = HideFlags.HideAndDontSave };
                    if (!_characterCloseupTexture.Create()) { CloseCharacterCloseup(); return false; }
                    var go = new GameObject("City character closeup camera");
                    go.transform.SetParent(_root.transform, false);
                    _characterCloseupCamera = go.AddComponent<Camera>();
                    _characterCloseupCamera.enabled = false;
                    _characterCloseupCamera.targetTexture = _characterCloseupTexture;
                    _characterCloseupCamera.clearFlags = CameraClearFlags.SolidColor;
                    _characterCloseupCamera.cullingMask = _camera.cullingMask & ~(1 << 31);
                    _characterCloseupCamera.nearClipPlane = .001f;
                    _characterCloseupCamera.farClipPlane = .35f;
                    _characterCloseupCamera.fieldOfView = 35f;
                    _characterCloseupCamera.aspect = 1.5f;
                    _characterCloseupCamera.allowHDR = false;
                    _characterCloseupCamera.allowMSAA = false;
                    var data = _characterCloseupCamera.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = false;
                    data.renderShadows = false;
                    if (_rendererIndex >= 0) data.SetRenderer(_rendererIndex);
                }
                _characterCloseupCamera.backgroundColor = _camera.backgroundColor;
                _characterCloseupCamera.transform.SetPositionAndRotation(position, rotation);
                _characterCloseupCamera.enabled = true;
                texture = _characterCloseupTexture;
                return true;
            }
            catch (System.Exception ex)
            {
                CloseCharacterCloseup();
                Debug.LogWarning("Character closeup unavailable: " + ex.Message);
                return false;
            }
        }

        public void CloseCharacterCloseup()
        {
            if (_characterCloseupCamera != null)
            {
                _characterCloseupCamera.enabled = false;
                _characterCloseupCamera.targetTexture = null;
                Destroy(_characterCloseupCamera.gameObject);
            }
            if (_characterCloseupTexture != null)
            {
                _characterCloseupTexture.Release();
                Destroy(_characterCloseupTexture);
            }
            _characterCloseupCamera = null;
            _characterCloseupTexture = null;
        }
    }
}
