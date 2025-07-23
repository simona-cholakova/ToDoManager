namespace WebApplication2.Services;

public class KernelService
{
    public static float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length) return 0f;

        float dot = 0f;
        float magA = 0f;
        float magB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dot += vectorA[i] * vectorB[i];
            magA += vectorA[i] * vectorA[i];
            magB += vectorB[i] * vectorB[i];
        }
            

        return dot / (float)(Math.Sqrt(magA) * Math.Sqrt(magB) + 1e-8); //Add epsilon to avoid division by zero
    }
}