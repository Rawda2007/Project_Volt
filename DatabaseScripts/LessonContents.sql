USE [VoltDB]
GO

/****** Object:  Table [LearningContent].[LessonContents]    Script Date: 9/11/2026 2:33:45 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [LearningContent].[LessonContents](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LessonId] [int] NOT NULL,
	[ContentTypeId] [int] NOT NULL,
	[Content] [nvarchar](max) NULL,
	[MediaUrl] [nvarchar](500) NULL,
	[SortOrder] [int] NOT NULL,
 CONSTRAINT [PK_ContentItems] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [LearningContent].[LessonContents]  WITH CHECK ADD  CONSTRAINT [FK_LessonContents_ContentTypes] FOREIGN KEY([ContentTypeId])
REFERENCES [LearningContent].[ContentTypes] ([Id])
GO

ALTER TABLE [LearningContent].[LessonContents] CHECK CONSTRAINT [FK_LessonContents_ContentTypes]
GO

ALTER TABLE [LearningContent].[LessonContents]  WITH CHECK ADD  CONSTRAINT [FK_LessonContents_Lessons] FOREIGN KEY([LessonId])
REFERENCES [LearningContent].[Lessons] ([Id])
GO

ALTER TABLE [LearningContent].[LessonContents] CHECK CONSTRAINT [FK_LessonContents_Lessons]
GO


