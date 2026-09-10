using System;
using System.Collections.Generic;

namespace AssessmentDA.Entities;

public partial class QuestionHint
{
    public long Id { get; set; }

    public long QuizAttemptMistakeId { get; set; }

    public string HintText { get; set; } = null!;

    public byte HintSequence { get; set; }

    public DateTime GeneratedAt { get; set; }

    public virtual QuizAttemptMistake QuizAttemptMistake { get; set; } = null!;
}
