namespace StableSwarmUI.Utils;

using System.IO;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Extensions.SkiaSharp;
using FFMpegCore.Pipes;
using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using ISImage = SixLabors.ImageSharp.Image;

/// <summary>
/// Represents a utility class for generating videos from images.
/// </summary>
public class Video {
/// <summary>
    /// Creates a sequence of video frames based on the provided files, duration, frames per second (fps), width, height, and duration scalar function.
    /// </summary>
    /// <param name="files">An array of file paths representing the frames of the video.</param>
    /// <param name="duration">The total duration of the video in seconds.</param>
    /// <param name="fps">The desired frames per second (fps) of the video.</param>
    /// <param name="width">The width of each video frame.</param>
    /// <param name="height">The height of each video frame.</param>
    /// <param name="durationScalarFn">A function that calculates the duration scalar based on the current frame index and the total number of frames.</param>
    /// <returns>An enumerable sequence of video frames.</returns>
    private static IEnumerable<IVideoFrame> CreateFrames(
        string[] files, 
        double duration, 
        double fps, 
        int width, 
        int height, 
        Func<double, int, double> durationScalarFn)
    {
        int targetFrameCount = (int)Math.Round(fps * duration);
        int frameCountTotal = 0;
        double averageFrameDuration = duration / files.Length;
        for (int i = 0; i < files.Length; i++)
        {
            var fileDuration = averageFrameDuration * durationScalarFn(i, files.Length);
            var frameCount = (int)Math.Round(fps * fileDuration);

            // Pad the last frame to reach the target frame count
            if (i == files.Length - 1 && frameCountTotal < targetFrameCount)
            {
                frameCount += targetFrameCount - frameCountTotal;
            }
            if (frameCount <= 0)
            {
                Logs.Info($"{i + 1}/{files.Length}");
                continue;
            }

            //Logs.Info($"durationScalarFn: {durationScalarFn(i, files.Length)}, averageFrameDuration = {averageFrameDuration}, fileDuration = {fileDuration}, frameCount = {frameCount}");

            using var stream = File.OpenRead(files[i]);
            var bitmap = SKBitmap.Decode(stream);
            for (int j = 0; j < frameCount; j++)
            {
                yield return new BitmapVideoFrameWrapper(bitmap);
            }
            frameCountTotal += frameCount;
            Logs.Info($"{i + 1}/{files.Length}");
        }
    }

    /// <summary>
    /// Generates a video from a collection of images.
    /// </summary>
    /// <param name="videoStream">The output video stream.</param>
    /// <param name="files">The array of image file paths.</param>
    /// <param name="duration">The duration of the video in seconds.</param>
    /// <param name="fps">The frames per second of the video.</param>
    /// <param name="width">The width of the video in pixels.</param>
    /// <param name="height">The height of the video in pixels.</param>
    /// <param name="durationScalarFn">A function that scales the duration of each frame.</param>
    /// <returns>True if the video generation is successful; otherwise, false.</returns>
    public static bool FromImages(
        MemoryStream videoStream, 
        string[] files, 
        double duration, 
        double fps, 
        int width, 
        int height, 
        Func<double, int, double> durationScalarFn)
    {
        // 8 Mbps, recommended by Google for streaming 1080p videos
        // +1 Mbps due to the nature of the displayed content (highly irregular pixel change delta between frames)
        const int bitrate = 8192 + 1024; // in Kbps

        var videoSink = new StreamPipeSink(videoStream);
        var frameSource = new RawVideoPipeSource(CreateFrames(files, duration, fps, width, height, durationScalarFn));
        return FFMpegArguments
            .FromPipeInput(frameSource)
            .OutputToPipe(videoSink, options => options
                .WithVideoBitrate(bitrate)
                .ForceFormat("mpegts")
            ).ProcessSynchronously();
    }

    /// <summary>
    /// Saves the video stream to a file at the specified output path.
    /// </summary>
    /// <param name="videoStream">The video stream to save.</param>
    /// <param name="outputVideoPath">The path where the output video file will be saved.</param>
    /// <param name="fps">The frames per second of the output video.</param>
    /// <returns>A boolean value indicating whether the video was successfully saved to the file.</returns>
    public static bool ToFile(
        MemoryStream videoStream,
        string outputVideoPath,
        string frameSmoothing,
        double fps
    )
    {
        // Output the video stream to a file
        videoStream.Position = 0;
        if (File.Exists(outputVideoPath))
        {
            File.Delete(outputVideoPath);
        }

        var customArgs = "";
        if (frameSmoothing == "fast")
        {
            customArgs = $"-vf minterpolate='fps={fps}:mi_mode=blend'";
        }
        else if (frameSmoothing == "quality")
        {
            customArgs = $"-vf minterpolate='fps={fps}:mi_mode=mci'";
        }

        return FFMpegArguments
            .FromPipeInput(new StreamPipeSource(videoStream), options => options
                .ForceFormat("mpegts")
            ).OutputToFile(outputVideoPath, true, options => options
                .WithCustomArgument(customArgs)
                .WithVideoCodec(VideoCodec.LibX264)
                .UsingMultithreading(true)
                .WithFastStart()
            ).ProcessSynchronously();
    }

    public static string[] frameEffects = ["ping", "pong", "ping-pong"];
    public static string[] frameEffectShapes = ["linear", "rounded"];
    public static string[] frameSmoothingOptions = ["off", "fast", "quality"];

    /// <summary>
    /// Generates a video from a sequence of images.
    /// </summary>
    /// <param name="wwwRoot">The root path of the web server.</param>
    /// <param name="inputPath">The path of the input image.</param>
    /// <param name="outputPath">The path where the output video will be saved.</param>
    /// <param name="frameEffect">The frame effect to apply to the video (e.g., "ping-pong", "ping", "pong").</param>
    /// <param name="frameEffectShape">The shape of the frame effect (e.g., "rounded").</param>
    /// <param name="frameSmoothing">A boolean value indicating whether to apply frame smoothing to the video.</param>
    /// <param name="duration">The duration of the video in seconds.</param>
    /// <returns>A <see cref="JObject"/> containing the path of the generated video or an error message.</returns>
    public static async Task<JObject> ImageAsVideo(
        string wwwRoot,
        string inputPath, 
        string outputPath,
        string frameEffect, 
        string frameEffectShape, 
        string frameSmoothing,
        int duration
        )
    {
        const double FPS = 30000/1001; // 29.97 FPS, the standard for mp4 videos

        // Validate the input parameters
        if (!File.Exists(inputPath))
        {
            Logs.Warning($"The provided path '{inputPath}' does not exist.");
            return new JObject() { ["error"] = "The provided path does not exist." };
        }
        if (!frameEffects.Contains(frameEffect))
        {
            Logs.Warning($"The frame effect '{frameEffect}' is invalid. It must be one of the following: {string.Join(", ", frameEffects)}.");
            return new JObject() { ["error"] = $"The frame effect must be one of the following: {string.Join(", ", frameEffects)}." };
        }
        if (!frameEffectShapes.Contains(frameEffectShape))
        {
            Logs.Warning($"The frame effect shape '{frameEffectShape}' is invalid. It must be one of the following: {string.Join(", ", frameEffectShapes)}.");
            return new JObject() { ["error"] = $"The frame effect shape must be one of the following: {string.Join(", ", frameEffectShapes)}." };
        }
        if (duration <= 0)
        {
            Logs.Warning($"The duration '{duration}' must be greater than 0.");
            return new JObject() { ["error"] = "The duration must be greater than 0." };
        }

        double periodDuration = duration;
        if (frameEffect == "ping-pong")
        {
            periodDuration *= 0.5;
        }

        // Analyse the provided image to get the output video resolution
        var image = ISImage.Load(inputPath);
        var inputWidth = image.Width;
        var inputHeight = image.Height;

        // Gather up a list of all the images in the current folder
        string ext = inputPath.AfterLast('.').ToLower();
        string folder = Path.GetDirectoryName(inputPath);
        string[] files = Directory.GetFiles(folder, $"*.{ext}", SearchOption.TopDirectoryOnly)
            // Filter out files that are not the same resolution as the input image
            .Where(f => {
                var img = ISImage.Load(f);
                return img.Width == inputWidth && img.Height == inputHeight;
            })
            .ToArray();

        // if there are not enough images in the folder or subfolders, return an error
        if (files.Length < 2)
        {
            Logs.Warning($"Not enough images with extension {ext} and resolution {inputWidth}x{inputHeight} in the folder '{folder}' to generate a video.");
            return new JObject() { ["error"] = "There are not enough images of that extension and resolution in the current folder to generate a video." };
        }

        // Sort the files by date of creation, so that the video is in the correct order.
        Array.Sort(files, (a, b) => File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));

        // Determine the maximum amount of images that can be displayed in the given duration
        int maxImages = (int)(periodDuration * FPS);
        // Ensure the `files` count is less than the `maxImages` by filtering
        if (files.Length > maxImages)
        {
            // Remove images evenly over the list to match the maxImages count
            var step = files.Length / maxImages;
            files = files.Where((_, i) => i % step == 0).ToArray();
            // If the files count is still greater than the maxImages, remove the difference from the middle
            if (files.Length > maxImages)
            {
                var mid = files.Length / 2;
                var midStep = (int)Math.Round(Math.Max(1, (files.Length - maxImages) / 2.0));
                files = files.Where((_, i) => i < mid - midStep || i > mid + midStep).ToArray();
            }
        }

        // Assign a generator function for the duration scalar based on the frame effect shape
        // These act as a magnitude scalar for the duration of each frame based on the linear frame duration average
        Func<double, int, double> durationScalarFn = (double x, int waveLength) => 1.0;
        if (frameEffectShape == "rounded")
        {
            // An inverted bell curve function, where the result is 1.5 at the start and end of the period, and 0.5 at the midpoint
            durationScalarFn = (double x, int waveLength) => {
                // normalize the x value to be between -1 and 1, where 0 is the midpoint of the wave
                var normX = ((x / waveLength) * 2) - 1;
                return -1 * (Math.Pow(normX, 4) - (2 * Math.Pow(normX, 2))) + 0.5;
            };
        }

        // Now, use the FFMpegCore library to generate a video from the images
        await using var videoStream = new MemoryStream();
        if (frameEffect == "ping-pong" || frameEffect == "ping")
        {
            if (!FromImages(videoStream, files, periodDuration, FPS, inputWidth, inputHeight, durationScalarFn))
            {
                return new JObject() { ["error"] = "Failed to generate ping video." };
            }
        }

        if (frameEffect == "ping-pong" || frameEffect == "pong")
        {
            if (!FromImages(videoStream, files.Reverse().ToArray(), periodDuration, FPS, inputWidth, inputHeight, durationScalarFn))
            {
                return new JObject() { ["error"] = "Failed to generate pong video." };
            }
        }

        // Output the video stream to a file
        videoStream.Position = 0;
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var concatResult = ToFile(videoStream, outputPath, frameSmoothing, FPS);
        if (concatResult)
        {
            // Return the path of the output video relative to the wwwroot
            return new JObject() { ["video"] = outputPath.Replace('\\', '/').Replace(wwwRoot.Replace('\\', '/'), "").TrimStart('/') };
        }
        else
        {
            Logs.Error($"Failed to generate video from images.");
            return new JObject() { ["error"] = "Failed to generate video." };
        }
    }
}
