using Runtime.Engine.Noise;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace NoiseTest
{
    public class NoiseDisplay : MonoBehaviour
    {
        [SerializeField] private int width = 256;
        [SerializeField] private int height = 256;

        public int2 offset;

        public RawImage rawImage;
        private Texture2D _noiseTexture;
        [SerializeField] private NoiseProfile.Settings settings;
        public float biomeScale = 0.0012f;
        public float caveScale = 0.03f; 
        public float pow = 3f;
        public bool showHumidity = true;
        public bool showTemperature = true;
        public bool showHeight = true;

        private void Start()
        {
            rawImage.texture = new Texture2D(width, height);
            _noiseTexture = (Texture2D)rawImage.texture;
            GenerateNoise();
        }


        private void GenerateNoise()
        {
            var noiseProfile = new NoiseProfile(settings);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float2 pos = new int2(x, y) + offset;
                    float tHeight = noiseProfile.GetNoise(pos);
                    float humidity = noise.cnoise((pos + 789f) * biomeScale);
                    float temperature = noise.cnoise((pos - 543f) * biomeScale);
                    float2 cave = noise.cellular2x2(new float2(pos.x * caveScale, pos.y * caveScale));

                    humidity = math.pow(humidity, pow);

                    Color color = new(showHeight ? cave.x is > .75f and < .8f ? 1 :0f : 0f,
                        showHumidity ? cave.y is > .75f and < .8f ? 1 :0f  : 0f,
                        showTemperature ? temperature : 0f);
                    _noiseTexture.SetPixel(x, y, color);
                }
            }

            _noiseTexture.Apply();
        }

        private void OnValidate()
        {
            rawImage.texture = new Texture2D(width, height);
            _noiseTexture = (Texture2D)rawImage.texture;
            GenerateNoise();
        }
    }
}