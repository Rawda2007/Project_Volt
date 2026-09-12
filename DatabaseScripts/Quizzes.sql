USE [VoltDB]
GO

/****** Object:  Table [Assessment].[Quizzes]    Script Date: 9/11/2026 2:28:01 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[Quizzes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Title] [nvarchar](300) NOT NULL,
	[Description] [nvarchar](max) NULL,
	[QuizType] [nvarchar](30) NOT NULL,
	[LevelId] [int] NULL,
	[LessonId] [int] NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](3) NOT NULL,
	[UpdatedAt] [datetime2](3) NULL,
 CONSTRAINT [PK_Quizzes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[Quizzes] ADD  CONSTRAINT [DF_Quizzes_QuizType]  DEFAULT ('Standalone') FOR [QuizType]
GO

ALTER TABLE [Assessment].[Quizzes] ADD  CONSTRAINT [DF_Quizzes_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO

ALTER TABLE [Assessment].[Quizzes] ADD  CONSTRAINT [DF_Quizzes_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Assessment].[Quizzes]  WITH CHECK ADD  CONSTRAINT [CK_Quizzes_QuizType] CHECK  (([QuizType]='Standalone' OR [QuizType]='LessonReview' OR [QuizType]='LessonQuiz' OR [QuizType]='LevelAssessment'))
GO

ALTER TABLE [Assessment].[Quizzes] CHECK CONSTRAINT [CK_Quizzes_QuizType]
GO

ALTER TABLE [Assessment].[Quizzes]  WITH CHECK ADD  CONSTRAINT [CK_Quizzes_TypeMatchesReference] CHECK  (([QuizType]='LevelAssessment' AND [LevelId] IS NOT NULL AND [LessonId] IS NULL OR ([QuizType]='LessonReview' OR [QuizType]='LessonQuiz') AND [LessonId] IS NOT NULL AND [LevelId] IS NULL OR [QuizType]='Standalone' AND [LevelId] IS NULL AND [LessonId] IS NULL))
GO

ALTER TABLE [Assessment].[Quizzes] CHECK CONSTRAINT [CK_Quizzes_TypeMatchesReference]
GO


