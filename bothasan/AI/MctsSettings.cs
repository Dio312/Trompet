// MCTS algoritması için ayar sınıfı.
// Zorluk seviyesine göre iterasyon sayısı ve zaman limitini belirler.

public enum AiDifficulty
{
    Easy,
    Normal,
    Hard
}

public class MctsSettings
{
    public int MaxIterations = 500;
    public double TimeLimitSeconds = 0.3;
    public double ExplorationConstant = 1.41;
    public int RolloutDepth = 20;
    public System.Random Rng;

    public MctsSettings(AiDifficulty difficulty = AiDifficulty.Normal)
    {
        Rng = new System.Random();

        switch (difficulty)
        {
            case AiDifficulty.Easy:
                MaxIterations = 150;
                TimeLimitSeconds = 0.15;
                RolloutDepth = 10;
                break;
            case AiDifficulty.Normal:
                MaxIterations = 500;
                TimeLimitSeconds = 0.3;
                RolloutDepth = 20;
                break;
            case AiDifficulty.Hard:
                MaxIterations = 1500;
                TimeLimitSeconds = 0.7;
                RolloutDepth = 30;
                break;
        }
    }
}
