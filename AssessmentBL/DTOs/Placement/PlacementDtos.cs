namespace AssessmentBL.DTOs.Placement
{
    /// <summary>GET /api/placement — whether the app should show the placement test.</summary>
    public class PlacementStatusDto
    {
        /// <summary>Required | Optional | InProgress | Completed | Unavailable.</summary>
        public string Status { get; set; } = null!;

        /// <summary>The active placement quiz. Null when Unavailable or Completed.</summary>
        public int? PlacementQuizId { get; set; }

        /// <summary>The open attempt (InProgress) or the placing attempt (Completed).</summary>
        public long? AttemptId { get; set; }

        /// <summary>Only when Completed.</summary>
        public PlacementResultDto? Result { get; set; }
    }

    /// <summary>Where the learner was placed, and why.</summary>
    public class PlacementResultDto
    {
        /// <summary>The Content-module level to start the learner at.</summary>
        public int LevelId { get; set; }

        /// <summary>Null only if the level has since been deleted.</summary>
        public string? LevelTitle { get; set; }

        public int? LevelOrder { get; set; }

        /// <summary>Overall score of the placement attempt.</summary>
        public decimal ScorePercentage { get; set; }

        /// <summary>The per-level mastery threshold that was applied.</summary>
        public decimal PassPercentage { get; set; }

        public DateTime PlacedAt { get; set; }

        /// <summary>Every level that had placement questions, easiest first.</summary>
        public List<PlacementLevelResultDto> Levels { get; set; } = new();
    }

    public class PlacementLevelResultDto
    {
        public int LevelId { get; set; }

        public string LevelTitle { get; set; } = null!;

        public int QuestionsAsked { get; set; }

        public int CorrectAnswers { get; set; }

        public decimal ScorePercentage { get; set; }

        /// <summary>ScorePercentage reached PassPercentage.</summary>
        public bool Mastered { get; set; }
    }
}
