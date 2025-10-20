using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Utils;

namespace Runtime.Shaders
{
    public class PostProcessManager : Singleton<PostProcessManager>
    {
        public Volume volume;

        private VolumeProfile _profile;

        private LiftGammaGain _liftGammaGain;
        private ColorAdjustments _colorAdjustments;

        private void OnEnable()
        {
            volume.profile = _profile;
            volume.profile.TryGet(out _liftGammaGain);
            volume.profile.TryGet(out _colorAdjustments);
        }

        public void SetLiftGammaGain(Vector4 all) => SetLiftGammaGain(all, all, all);

        public void SetLiftGammaGain(Vector4 lift, Vector4 gamma, Vector4 gain)
        {
            if (!_liftGammaGain) return;
            _liftGammaGain.active = true;
            _liftGammaGain.lift.value = lift;
            _liftGammaGain.gamma.value = gamma;
            _liftGammaGain.gain.value = gain;
        }

        public void ResetLiftGammaGain()
        {
            if (!_liftGammaGain || !_liftGammaGain.active) return;
            _liftGammaGain.active = false;
            _liftGammaGain.lift.value = Vector4.zero;
            _liftGammaGain.gamma.value = Vector4.one;
            _liftGammaGain.gain.value = Vector4.one;
        }

        public void SetColorAdjustments(float contrast, float saturation)
        {
            if (!_colorAdjustments) return;
            _colorAdjustments.active = true;
            _colorAdjustments.contrast.value = contrast;
            _colorAdjustments.saturation.value = saturation;
        }

        public void ResetColorAdjustments()
        {
            if (!_colorAdjustments || !_colorAdjustments.active) return;
            _colorAdjustments.active = false;
            _colorAdjustments.contrast.value = 0f;
            _colorAdjustments.saturation.value = 0f;
        }
    }
}