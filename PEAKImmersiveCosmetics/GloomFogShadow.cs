//PEAK Immersive Cosmetics
//Distributed under the MIT License @ https://github.com/NonRadical/PEAKImmersiveCosmetics

using UnityEngine;

namespace PEAKImmersiveCosmetics
{
    /**
     * Applies the ModifyGloomFogDensity hook to the gloom's height fog. The fog is a
     * global shader parameter written by DayNightManager, with no per-character funnel to
     * patch, so this component shadows the value: each LateUpdate it re-reads the global,
     * treats any value it did not write itself as the game's fresh raw value, and writes
     * the scaled result on top. Purely visual and local to this machine.
     */
    internal class GloomFogShadow : MonoBehaviour
    {
        private const string FogParam = "HeightFogAmount";
        private const float Epsilon = 0.0001f;

        private float _raw;
        private float _lastWritten;
        private bool _active;

        private void LateUpdate()
        {
            try
            {
                float current = Shader.GetGlobalFloat(FogParam);
                // Anything we did not write ourselves is a fresh game value.
                if (!_active || Mathf.Abs(current - _lastWritten) > Epsilon)
                {
                    _raw = current;
                }

                Character local = Character.localCharacter;
                bool wantModify = local != null && !local.isBot
                    && EffectHelpers.InGloom()
                    && _raw > Epsilon;
                float target = wantModify ? EffectResolver.ModifyGloomFogDensity(local, _raw) : _raw;

                if (wantModify && Mathf.Abs(target - _raw) > Epsilon)
                {
                    if (Mathf.Abs(current - target) > Epsilon)
                    {
                        Shader.SetGlobalFloat(FogParam, target);
                    }
                    _lastWritten = target;
                    _active = true;
                }
                else if (_active)
                {
                    Shader.SetGlobalFloat(FogParam, _raw);
                    _active = false;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Gloom fog effect failed: {e}");
                enabled = false;
            }
        }
    }
}
