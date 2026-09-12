USE [VoltDB]
GO

/****** Object:  Table [Assessment].[QuizAttemptEssayAnswers]    Script Date: 9/11/2026 2:21:42 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[QuizAttemptEssayAnswers](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[QuizAttemptId] [bigint] NOT NULL,
	[QuestionId] [int] NOT NULL,
	[AnswerText] [nvarchar](max) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[AwardedPoints] [tinyint] NULL,
	[Feedback] [nvarchar](max) NULL,
	[GradedBy] [nvarchar](20) NULL,
	[GradedAt] [datetime2](3) NULL,
	[CreatedAt] [datetime2](3) NOT NULL,
 CONSTRAINT [PK_QuizAttemptEssayAnswers] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_QuizAttemptEssayAnswers_AttemptId_QuestionId] UNIQUE NONCLUSTERED 
(
	[QuizAttemptId] ASC,
	[QuestionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers] ADD  CONSTRAINT [DF_QuizAttemptEssayAnswers_Status]  DEFAULT ('Pending') FOR [Status]
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers] ADD  CONSTRAINT [DF_QuizAttemptEssayAnswers_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptEssayAnswers_Questions] FOREIGN KEY([QuestionId])
REFERENCES [Assessment].[Questions] ([Id])
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers] CHECK CONSTRAINT [FK_QuizAttemptEssayAnswers_Questions]
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptEssayAnswers_QuizAttemptQuestions] FOREIGN KEY([QuizAttemptId], [QuestionId])
REFERENCES [Assessment].[QuizAttemptQuestions] ([QuizAttemptId], [QuestionId])
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers] CHECK CONSTRAINT [FK_QuizAttemptEssayAnswers_QuizAttemptQuestions]
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptEssayAnswers_QuizAttempts] FOREIGN KEY([QuizAttemptId])
REFERENCES [Assessment].[QuizAttempts] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers] CHECK CONSTRAINT [FK_QuizAttemptEssayAnswers_QuizAttempts]
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttemptEssayAnswers_GradedBy] CHECK  (([GradedBy] IS NULL OR ([GradedBy]='Ai' OR [GradedBy]='Human')))
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers] CHECK CONSTRAINT [CK_QuizAttemptEssayAnswers_GradedBy]
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttemptEssayAnswers_GradedIsComplete] CHECK  (([Status]='Graded' AND [AwardedPoints] IS NOT NULL AND [GradedAt] IS NOT NULL AND [GradedBy] IS NOT NULL OR [Status]<>'Graded' AND [AwardedPoints] IS NULL AND [GradedAt] IS NULL AND [GradedBy] IS NULL))
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers] CHECK CONSTRAINT [CK_QuizAttemptEssayAnswers_GradedIsComplete]
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttemptEssayAnswers_Status] CHECK  (([Status]='Skipped' OR [Status]='Graded' OR [Status]='Pending'))
GO

ALTER TABLE [Assessment].[QuizAttemptEssayAnswers] CHECK CONSTRAINT [CK_QuizAttemptEssayAnswers_Status]
GO


