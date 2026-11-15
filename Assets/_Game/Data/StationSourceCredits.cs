using System;
using System.Collections.Generic;

namespace LKS
{
    public readonly struct StationSourceCredit
    {
        public string Id { get; }
        public string Attribution { get; }
        public string SourceUrl { get; }

        internal StationSourceCredit(string id, string attribution, string sourceUrl)
        {
            Id = id;
            Attribution = attribution;
            SourceUrl = sourceUrl;
        }
    }

    public static class StationSourceCredits
    {
        public const int Version = 1;

        public static IReadOnlyList<StationSourceCredit> All { get; } = Array.AsReadOnly(new[]
        {
            new StationSourceCredit("nasa-donki-sep-2017",
                "NASA CCMC DONKI and contributing instruments. Educational use. No NASA endorsement.",
                "https://ccmc.gsfc.nasa.gov/tools/DONKI/#donki-rules-of-the-road"),
            new StationSourceCredit("ntrs.20210013268", "Eigenbrode et al. (2018), Curiosity SAM mudstone study.",
                "https://ntrs.nasa.gov/citations/20210013268"),
            new StationSourceCredit("ntrs.20205001823", "Rampe et al. (2020), Curiosity CheMin mineralogy study.",
                "https://ntrs.nasa.gov/citations/20205001823"),
            new StationSourceCredit("ntrs.20130011130", "Eigenbrode et al. (2013), Curiosity SAM background control.",
                "https://ntrs.nasa.gov/citations/20130011130"),
            new StationSourceCredit("csa-food", "Canadian Space Agency, Food production on the Moon and in remote areas.",
                "https://www.asc-csa.gc.ca/eng/youth-educators/toolkits/food-production/"),
            new StationSourceCredit("metal_plate", "Metal Plate normal map by Rob Tuytel, Poly Haven (CC0).",
                "https://polyhaven.com/a/metal_plate"),
            new StationSourceCredit("rough_concrete", "Rough Concrete normal map by Dimitrios Savva, Poly Haven (CC0).",
                "https://polyhaven.com/a/rough_concrete"),
            new StationSourceCredit("unity-engine", "Made with Unity. Includes Unity engine and URP resources.",
                "https://unity.com/legal/terms-of-service/software"),
            new StationSourceCredit("asterion-prototype-v1", "Original art, story and engineering models by the LAST KNOWN STATE team.",
                null)
        });
    }
}
