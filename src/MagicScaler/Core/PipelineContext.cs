﻿using System;
using System.Linq;
using System.Collections.Generic;

namespace PhotoSauce.MagicScaler
{
	internal class PipelineContext : IDisposable
	{
		internal class PlanarPipelineContext
		{
			public PixelSource SourceY;
			public PixelSource SourceCb;
			public PixelSource SourceCr;

			public PlanarPipelineContext(PixelSource sourceY, PixelSource sourceCb, PixelSource sourceCr)
			{
				SourceY = sourceY;
				SourceCb = sourceCb;
				SourceCr = sourceCr;
			}
		}

		private readonly Stack<IDisposable> disposeHandles = new Stack<IDisposable>(16);

		private PixelSource? source;
		private List<PixelSource>? allSources;
		private WicPipelineContext? wicContext;

		public ProcessImageSettings Settings { get; }
		public ProcessImageSettings UsedSettings { get; private set; }
		public Orientation Orientation { get; set; }

		public IImageContainer ImageContainer { get; set; }
		public IImageFrame ImageFrame { get; set; }

		public PlanarPipelineContext? PlanarContext { get; set; }

		public ColorProfile? SourceColorProfile { get; set; }
		public ColorProfile? DestColorProfile { get; set; }

		public IEnumerable<PixelSourceStats> Stats => allSources?.Select(s => s.Stats!) ?? Enumerable.Empty<PixelSourceStats>();

		public WicPipelineContext WicContext => wicContext ??= AddDispose(new WicPipelineContext());

		public PixelSource Source
		{
			get => source ?? NoopPixelSource.Instance;
			set
			{
				source = value;

				if (MagicImageProcessor.EnablePixelSourceStats)
				{
					allSources ??= new List<PixelSource>(8);
					if (!allSources.Contains(source))
						allSources.Add(source);
				}
			}
		}

		public PipelineContext(ProcessImageSettings settings)
		{
			Settings = settings.Clone();

			// HACK this quiets the nullable warnings for now but needs refactoring
			UsedSettings = null!;
			ImageContainer = null!;
			ImageFrame = null!;
		}

		public T AddDispose<T>(T disposeHandle) where T : IDisposable
		{
			disposeHandles.Push(disposeHandle);

			return disposeHandle;
		}

		public void FinalizeSettings()
		{
			Orientation = Settings.OrientationMode == OrientationMode.Normalize ? ImageFrame.ExifOrientation : Orientation.Normal;

			if (!Settings.Normalized)
			{
				Settings.Fixup(Source.Width, Source.Height, Orientation.SwapsDimensions());

				if (Settings.SaveFormat == FileFormat.Auto)
					Settings.SetSaveFormat(ImageContainer.ContainerFormat, Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None);
			}

			UsedSettings = Settings.Clone();
		}

		public void Dispose()
		{
			while (disposeHandles.Count > 0)
				disposeHandles.Pop().Dispose();

			ImageFrame?.Dispose();
		}
	}
}
