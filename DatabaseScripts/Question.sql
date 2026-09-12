USE [VoltDB]
GO

/****** Object:  Table [Assessment].[Questions]    Script Date: 9/11/2026 2:20:33 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[Questions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[QuizId] [int] NOT NULL,
	[TopicId] [int] NOT NULL,
	[QuestionText] [nvarchar](max) NOT NULL,
	[Difficulty] [nvarchar](20) NOT NULL,
	[DisplayOrder] [smallint] NOT NULL,
	[Points] [tinyint] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](3) NOT NULL,
	[QuestionType] [nvarchar](30) NOT NULL,
	[ImageUrl] [nvarchar](max) NULL,
	[ImageDescription] [nvarchar](max) NULL,
 CONSTRAINT [PK_Questions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Questions_QuizId_DisplayOrder] UNIQUE NONCLUSTERED 
(
	[QuizId] ASC,
	[DisplayOrder] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[Questions] ADD  CONSTRAINT [DF_Questions_Difficulty]  DEFAULT ('Medium') FOR [Difficulty]
GO

ALTER TABLE [Assessment].[Questions] ADD  CONSTRAINT [DF_Questions_DisplayOrder]  DEFAULT ((0)) FOR [DisplayOrder]
GO

ALTER TABLE [Assessment].[Questions] ADD  CONSTRAINT [DF_Questions_Points]  DEFAULT ((1)) FOR [Points]
GO

ALTER TABLE [Assessment].[Questions] ADD  CONSTRAINT [DF_Questions_IsActive]  DEFAULT ((0)) FOR [IsActive]
GO

ALTER TABLE [Assessment].[Questions] ADD  CONSTRAINT [DF_Questions_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Assessment].[Questions] ADD  CONSTRAINT [DF_Questions_QuestionType]  DEFAULT ('MultipleChoice') FOR [QuestionType]
GO

ALTER TABLE [Assessment].[Questions]  WITH CHECK ADD  CONSTRAINT [FK_Questions_Quizzes] FOREIGN KEY([QuizId])
REFERENCES [Assessment].[Quizzes] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[Questions] CHECK CONSTRAINT [FK_Questions_Quizzes]
GO

ALTER TABLE [Assessment].[Questions]  WITH CHECK ADD  CONSTRAINT [FK_Questions_Topics] FOREIGN KEY([TopicId])
REFERENCES [Assessment].[Topics] ([Id])
GO

ALTER TABLE [Assessment].[Questions] CHECK CONSTRAINT [FK_Questions_Topics]
GO

ALTER TABLE [Assessment].[Questions]  WITH CHECK ADD  CONSTRAINT [CK_Questions_Difficulty] CHECK  (([Difficulty]='Advanced' OR [Difficulty]='Hard' OR [Difficulty]='Medium' OR [Difficulty]='Easy'))
GO

ALTER TABLE [Assessment].[Questions] CHECK CONSTRAINT [CK_Questions_Difficulty]
GO

ALTER TABLE [Assessment].[Questions]  WITH CHECK ADD  CONSTRAINT [CK_Questions_Points] CHECK  (([Points]>(0)))
GO

ALTER TABLE [Assessment].[Questions] CHECK CONSTRAINT [CK_Questions_Points]
GO

ALTER TABLE [Assessment].[Questions]  WITH CHECK ADD  CONSTRAINT [CK_Questions_QuestionType] CHECK  (([QuestionType]='Essay' OR [QuestionType]='TrueFalse' OR [QuestionType]='MultipleChoice'))
GO

ALTER TABLE [Assessment].[Questions] CHECK CONSTRAINT [CK_Questions_QuestionType]
GO


