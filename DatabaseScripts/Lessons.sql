USE [VoltDB]
GO

/****** Object:  Table [LearningContent].[Lessons]    Script Date: 9/11/2026 2:34:22 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [LearningContent].[Lessons](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LevelId] [int] NOT NULL,
	[Title] [nvarchar](200) NOT NULL,
	[Description] [nvarchar](1000) NULL,
	[SortOrder] [int] NOT NULL,
	[IsPublished] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Lessons] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [LearningContent].[Lessons] ADD  CONSTRAINT [DF_Lessons_IsPublished]  DEFAULT ((0)) FOR [IsPublished]
GO

ALTER TABLE [LearningContent].[Lessons] ADD  CONSTRAINT [DF_Lessons_CreatedAt]  DEFAULT (getutcdate()) FOR [CreatedAt]
GO

ALTER TABLE [LearningContent].[Lessons]  WITH CHECK ADD  CONSTRAINT [FK_Lessons_Levels] FOREIGN KEY([LevelId])
REFERENCES [LearningContent].[Levels] ([Id])
GO

ALTER TABLE [LearningContent].[Lessons] CHECK CONSTRAINT [FK_Lessons_Levels]
GO


