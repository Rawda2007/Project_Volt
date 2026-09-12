USE [VoltDB]
GO

/****** Object:  Table [Assessment].[QuizAttempts]    Script Date: 9/11/2026 2:26:51 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[QuizAttempts](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[QuizId] [int] NOT NULL,
	[QuestionsAnsweredCount] [smallint] NOT NULL,
	[TotalQuestionsAtAttempt] [smallint] NOT NULL,
	[CorrectAnswersCount] [smallint] NOT NULL,
	[WrongAnswersCount]  AS ([QuestionsAnsweredCount]-[CorrectAnswersCount]) PERSISTED,
	[ScorePercentage] [decimal](5, 2) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[StartedAt] [datetime2](3) NOT NULL,
	[CompletedAt] [datetime2](3) NULL,
	[DurationSeconds]  AS (datediff(second,[StartedAt],[CompletedAt])) PERSISTED,
	[PreviousAttemptId] [bigint] NULL,
	[RowVersion] [timestamp] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
 CONSTRAINT [PK_QuizAttempts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_QuizAttempts_PreviousAttemptId] UNIQUE NONCLUSTERED 
(
	[PreviousAttemptId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [Assessment].[QuizAttempts] ADD  CONSTRAINT [DF_QuizAttempts_Status]  DEFAULT ('InProgress') FOR [Status]
GO

ALTER TABLE [Assessment].[QuizAttempts] ADD  CONSTRAINT [DF_QuizAttempts_StartedAt]  DEFAULT (sysutcdatetime()) FOR [StartedAt]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttempts_PreviousAttempt] FOREIGN KEY([PreviousAttemptId])
REFERENCES [Assessment].[QuizAttempts] ([Id])
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [FK_QuizAttempts_PreviousAttempt]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttempts_Quizzes] FOREIGN KEY([QuizId])
REFERENCES [Assessment].[Quizzes] ([Id])
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [FK_QuizAttempts_Quizzes]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttempts_AnsweredVsTotal] CHECK  (([QuestionsAnsweredCount]<=[TotalQuestionsAtAttempt]))
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [CK_QuizAttempts_AnsweredVsTotal]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttempts_CompletedAfterStarted] CHECK  (([CompletedAt] IS NULL OR [CompletedAt]>=[StartedAt]))
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [CK_QuizAttempts_CompletedAfterStarted]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttempts_CompletedRequiresAllAnswered] CHECK  (([Status]<>'Completed' OR [QuestionsAnsweredCount]=[TotalQuestionsAtAttempt] AND [CompletedAt] IS NOT NULL))
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [CK_QuizAttempts_CompletedRequiresAllAnswered]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttempts_CorrectVsAnswered] CHECK  (([CorrectAnswersCount]>=(0) AND [CorrectAnswersCount]<=[QuestionsAnsweredCount]))
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [CK_QuizAttempts_CorrectVsAnswered]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttempts_NotSelfReferencing] CHECK  (([PreviousAttemptId] IS NULL OR [PreviousAttemptId]<>[Id]))
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [CK_QuizAttempts_NotSelfReferencing]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttempts_ScorePercentage] CHECK  (([ScorePercentage]>=(0) AND [ScorePercentage]<=(100)))
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [CK_QuizAttempts_ScorePercentage]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttempts_Status] CHECK  (([Status]='Abandoned' OR [Status]='Completed' OR [Status]='InProgress'))
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [CK_QuizAttempts_Status]
GO

ALTER TABLE [Assessment].[QuizAttempts]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttempts_TotalQuestionsPositive] CHECK  (([TotalQuestionsAtAttempt]>(0)))
GO

ALTER TABLE [Assessment].[QuizAttempts] CHECK CONSTRAINT [CK_QuizAttempts_TotalQuestionsPositive]
GO


