public static class BiomeTemperature
{
    public static int Of(BiomeType biome)
    {
        switch (biome)
        {
            case BiomeType.Ice:     return -2;
            case BiomeType.Tundra:  return -1;
            case BiomeType.Savanna: return  1;
            case BiomeType.Desert:  return  2;
            default:                return  0;
        }
    }
}
