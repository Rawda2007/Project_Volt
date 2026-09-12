USE [VoltDB]
GO

/****** Object:  Table [Assessment].[UserTopicStats]    Script Date: 9/11/2026 2:30:34 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[UserTopicStats](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[TopicId] [int] NOT NULL,
	[Difficulty] [nvarchar](20) NOT NULL,
	[QuestionsAnsweredCount] [int] NOT NULL,
	[CorrectCount] [int] NOT NULL,
	[WrongCount]  AS ([QuestionsAnsweredCount]-[CorrectCount]) PERSISTED,
	[HintsUsedCount] [int] NOT NULL,
	[LastQuizAttemptId] [bigint] NULL,
	[LastPracticedAt] [datetime2](3) NULL,
	[UpdatedAt] [datetime2](3) NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
 CONSTRAINT [PK_UserTopicStats] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_UserTopicStats_UserId_TopicId_Difficulty] UNIQUE NONCLUSTERED 
(
	[UserId] ASC,
	[TopicId] ASC,
	[Difficulty] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [Assessment].[UserTopicStats] ADD  CONSTRAINT [DF_UserTopicStats_QAC]  DEFAULT ((0)) FOR [QuestionsAnsweredCount]
GO

ALTER TABLE [Assessment].[UserTopicStats] ADD  CONSTRAINT [DF_UserTopicStats_CC]  DEFAULT ((0)) FOR [CorrectCount]
GO

ALTER TABLE [Assessment].[UserTopicStats] ADD  CONSTRAINT [DF_UserTopicStats_HUC]  DEFAULT ((0)) FOR [HintsUsedCount]
GO

ALTER TABLE [Assessment].[UserTopicStats] ADD  CONSTRAINT [DF_UserTopicStats_UpdatedAt]  DEFAULT (sysutcdatetime()) FOR [UpdatedAt]
GO

ALTER TABLE [Assessment].[UserTopicStats]  WITH CHECK ADD  CONSTRAINT [FK_UserTopicStats_QuizAttempts] FOREIGN KEY([LastQuizAttemptId])
REFERENCES [Assessment].[QuizAttempts] ([Id])
ON DELETE SET NULL
GO

ALTER TABLE [Assessment].[UserTopicStats] CHECK CONSTRAINT [FK_UserTopicStats_QuizAttempts]
GO

ALTER TABLE [Assessment].[UserTopicStats]  WITH CHECK ADD  CONSTRAINT [FK_UserTopicStats_Topics] FOREIGN KEY([TopicId])
REFERENCES [Assessment].[Topics] ([Id])
GO

ALTER TABLE [Assessment].[UserTopicStats] CHECK CONSTRAINT [FK_UserTopicStats_Topics]
GO

ALTER TABLE [Assessment].[UserTopicStats]  WITH CHECK ADD  CONSTRAINT [CK_UserTopicStats_CorrectCountNonNegative] CHECK  (([CorrectCount]>=(0)))
GO

ALTER TABLE [Assessment].[UserTopicStats] CHECK CONSTRAINT [CK_UserTopicStats_CorrectCountNonNegative]
GO

ALTER TABLE [Assessment].[UserTopicStats]  WITH CHECK ADD  CONSTRAINT [CK_UserTopicStats_CorrectVsAnswered] CHECK  (([CorrectCount]<=[QuestionsAnsweredCount]))
GO

ALTER TABLE [Assessment].[UserTopicStats] CHECK CONSTRAINT [CK_UserTopicStats_CorrectVsAnswered]
GO

ALTER TABLE [Assessment].[UserTopicStats]  WITH CHECK ADD  CONSTRAINT [CK_UserTopicStats_Difficulty] CHECK  (([Difficulty]='Advanced' OR [Difficulty]='Hard' OR [Difficulty]='Medium' OR [Difficulty]='Easy'))
GO

ALTER TABLE [Assessment].[UserTopicStats] CHECK CONSTRAINT [CK_UserTopicStats_Difficulty]
GO

ALTER TABLE [Assessment].[UserTopicStats]  WITH CHECK ADD  CONSTRAINT [CK_UserTopicStats_HintsUsedNonNegative] CHECK  (([HintsUsedCount]>=(0)))
GO

ALTER TABLE [Assessment].[UserTopicStats] CHECK CONSTRAINT [CK_UserTopicStats_HintsUsedNonNegative]
GO

ALTER TABLE [Assessment].[UserTopicStats]  WITH CHECK ADD  CONSTRAINT [CK_UserTopicStats_QuestionsAnsweredNonNegative] CHECK  (([QuestionsAnsweredCount]>=(0)))
GO

ALTER TABLE [Assessment].[UserTopicStats] CHECK CONSTRAINT [CK_UserTopicStats_QuestionsAnsweredNonNegative]
GO


