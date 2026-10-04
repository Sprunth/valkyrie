using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Assets.Scripts.Tts
{
    // Voice style vectors loaded from a Supertonic voice_styles/*.json file
    public class SupertonicVoice
    {
        public float[] Ttl { get; private set; }
        public int[] TtlShape { get; private set; }
        public float[] Dp { get; private set; }
        public int[] DpShape { get; private set; }

        public static SupertonicVoice Load(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public static SupertonicVoice Parse(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var ttl = (Dictionary<string, object>)root["style_ttl"];
            var dp = (Dictionary<string, object>)root["style_dp"];
            return new SupertonicVoice
            {
                Ttl = Flatten(ttl["data"]).ToArray(),
                TtlShape = ((List<object>)ttl["dims"]).Select(d => (int)(double)d).ToArray(),
                Dp = Flatten(dp["data"]).ToArray(),
                DpShape = ((List<object>)dp["dims"]).Select(d => (int)(double)d).ToArray()
            };
        }

        private static IEnumerable<float> Flatten(object node)
        {
            var list = node as List<object>;
            if (list == null)
            {
                return new[] { (float)(double)node };
            }
            return list.SelectMany(Flatten);
        }
    }
}
