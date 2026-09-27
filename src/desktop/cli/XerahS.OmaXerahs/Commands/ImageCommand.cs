#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.CommandLine;
using XerahS.Media;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;

namespace XerahS.OmaXerahs.Commands;

/// <summary>Batch image tools (resize, convert, watermark) sharing the XerahS Image tools' engine.</summary>
internal static class ImageCommand
{
    internal static Command Create()
    {
        var command = new Command("image", "Resize, convert or watermark image files (same engine as the XerahS Image tools).");
        command.Add(CreateResize());
        command.Add(CreateConvert());
        command.Add(CreateWatermark());
        command.Add(CreateGif());
        return command;
    }

    private static Command CreateResize()
    {
        var command = new Command("resize", "Resize images to WIDTH x HEIGHT.");
        var width = new Option<int>("--width") { Description = "Target width in pixels.", Required = true };
        var height = new Option<int>("--height") { Description = "Target height in pixels.", Required = true };
        var mode = new Option<ImageResizeMode>("--mode") { Description = "fit (pad), fill (crop) or stretch.", DefaultValueFactory = _ => ImageResizeMode.Fit };
        command.Add(width);
        command.Add(height);
        command.Add(mode);
        return Build(command, ImageBatchOperation.Resize, parseResult => new ImageBatchOptions
        {
            Operation = ImageBatchOperation.Resize,
            Width = parseResult.GetValue(width),
            Height = parseResult.GetValue(height),
            ResizeMode = parseResult.GetValue(mode)
        });
    }

    private static Command CreateConvert()
    {
        var command = new Command("convert", "Convert images to another format (--format png|jpeg|webp).");
        return Build(command, ImageBatchOperation.Convert, _ => new ImageBatchOptions { Operation = ImageBatchOperation.Convert });
    }

    private static Command CreateWatermark()
    {
        var command = new Command("watermark", "Add a text (--text) or image (--image) watermark.");
        var text = new Option<string?>("--text") { Description = "Watermark text." };
        var image = new Option<string?>("--image") { Description = "Watermark image file (instead of --text)." };
        var position = new Option<ImageWatermarkPosition>("--position") { DefaultValueFactory = _ => ImageWatermarkPosition.BottomRight, Description = "TopLeft ... BottomRight, Center." };
        var opacity = new Option<int>("--opacity") { DefaultValueFactory = _ => 60, Description = "0-100." };
        var margin = new Option<int>("--margin") { DefaultValueFactory = _ => 16 };
        var textSize = new Option<float>("--text-size") { DefaultValueFactory = _ => 32 };
        var color = new Option<string>("--color") { DefaultValueFactory = _ => "#FFFFFF", Description = "Text color, e.g. #FFFFFF." };
        var scale = new Option<int>("--image-scale") { DefaultValueFactory = _ => 20, Description = "Watermark image size, % of the photo." };
        var rotation = new Option<float>("--rotation") { DefaultValueFactory = _ => 0 };
        foreach (Option option in new Option[] { text, image, position, opacity, margin, textSize, color, scale, rotation })
        {
            command.Add(option);
        }

        return Build(command, ImageBatchOperation.Watermark, parseResult =>
        {
            string? watermarkImage = parseResult.GetValue(image);
            string? watermarkText = parseResult.GetValue(text);
            if (string.IsNullOrWhiteSpace(watermarkText) == string.IsNullOrWhiteSpace(watermarkImage))
            {
                throw new ArgumentException("Pass exactly one of --text or --image.");
            }

            if (!ImageBatchService.TryParseColor(parseResult.GetValue(color), out var textColor))
            {
                throw new ArgumentException("--color must be a color such as #FFFFFF.");
            }

            return new ImageBatchOptions
            {
                Operation = ImageBatchOperation.Watermark,
                Watermark = new ImageWatermarkOptions
                {
                    Type = string.IsNullOrWhiteSpace(watermarkImage) ? ImageWatermarkType.Text : ImageWatermarkType.Image,
                    Text = watermarkText ?? string.Empty,
                    ImagePath = string.IsNullOrWhiteSpace(watermarkImage) ? null : Path.GetFullPath(watermarkImage),
                    Position = parseResult.GetValue(position),
                    Opacity = parseResult.GetValue(opacity),
                    Margin = parseResult.GetValue(margin),
                    TextSize = parseResult.GetValue(textSize),
                    TextColor = textColor,
                    ImageScale = parseResult.GetValue(scale),
                    Rotation = parseResult.GetValue(rotation)
                }
            };
        });
    }

    private static Command CreateGif()
    {
        var command = new Command("gif", "Combine two or more images, in the given order, into an animated GIF.");
        var files = new Argument<string[]>("files") { Description = "Frame images, in order.", Arity = new ArgumentArity(2, 100000) };
        var output = new Option<string>("--output", "-o") { Description = "Output .gif path.", Required = true };
        var delay = new Option<int>("--delay") { DefaultValueFactory = _ => 500, Description = "Milliseconds per frame (min 10)." };
        var repeat = new Option<int?>("--repeat") { Description = "Play this many extra times instead of looping forever (0 = play once)." };
        var maxSize = new Option<int>("--max-size") { DefaultValueFactory = _ => 0, Description = "Longest side in pixels (0 = first image's size)." };
        var jsonOption = JsonStdout.CreateJsonOption();
        foreach (Symbol symbol in new Symbol[] { files, output, delay, repeat, maxSize, jsonOption })
        {
            if (symbol is Argument argument) command.Add(argument);
            else command.Add((Option)symbol);
        }

        command.SetAction(async parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            string outputPath = Path.GetFullPath(parseResult.GetValue(output)!);
            string[] frames = (parseResult.GetValue(files) ?? []).Select(Path.GetFullPath).ToArray();
            string? missing = frames.FirstOrDefault(f => !File.Exists(f));
            if (missing != null)
            {
                return JsonStdout.WriteFailureAndExit(CliErrorCodes.InvalidPath, $"File not found: {missing}");
            }

            string ffmpegPath = XerahS.Common.PathsManager.GetFFmpegPath();
            if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                return JsonStdout.WriteFailureAndExit(CliErrorCodes.NotReady, "FFmpeg not found. Install FFmpeg (e.g. pacman -S ffmpeg).");
            }

            int? repeatCount = parseResult.GetValue(repeat);
            var options = new AnimatedGifOptions
            {
                DelayMilliseconds = parseResult.GetValue(delay),
                Loop = repeatCount == null,
                RepeatCount = repeatCount ?? 0,
                MaxDimension = parseResult.GetValue(maxSize)
            };

            try
            {
                await new AnimatedGifMakerService(ffmpegPath).CreateAsync(frames, outputPath, options);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
            {
                return JsonStdout.WriteFailureAndExit(CliErrorCodes.InvalidValue, ex.Message.Split('\n')[0]);
            }

            JsonStdout.Write(new ImageBatchResponse
            {
                Ok = true,
                Outputs = [new ImageOutput { Source = frames[0], Output = outputPath }]
            });
            return 0;
        });
        return command;
    }

    /// <summary>Adds the shared file/output options and runs the batch.</summary>
    private static Command Build(Command command, ImageBatchOperation operation, Func<ParseResult, ImageBatchOptions> createOptions)
    {
        var files = new Argument<string[]>("files") { Description = "Image files.", Arity = ArgumentArity.OneOrMore };
        var format = new Option<ImageBatchOutputFormat>("--format") { DefaultValueFactory = _ => ImageBatchOutputFormat.Png, Description = "png, jpeg or webp." };
        var quality = new Option<int>("--quality") { DefaultValueFactory = _ => 90, Description = "1-100 for jpeg/webp." };
        var background = new Option<string>("--background") { DefaultValueFactory = _ => "#FFFFFF", Description = "Fill for transparency when saving jpeg." };
        var outputFolder = new Option<string?>("--output-folder") { Description = "Where to write results (default: next to each image)." };
        var namePattern = new Option<string?>("--name") { Description = "Output file name without extension; $filename is the source name." };
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(files);
        command.Add(format);
        command.Add(quality);
        command.Add(background);
        command.Add(outputFolder);
        command.Add(namePattern);
        command.Add(jsonOption);

        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            ImageBatchOptions options;
            try
            {
                if (!ImageBatchService.TryParseColor(parseResult.GetValue(background), out var backgroundColor))
                {
                    throw new ArgumentException("--background must be a color such as #FFFFFF.");
                }

                string? folder = parseResult.GetValue(outputFolder);
                options = createOptions(parseResult) with
                {
                    Format = parseResult.GetValue(format),
                    Quality = parseResult.GetValue(quality),
                    BackgroundColor = backgroundColor,
                    OutputFolder = string.IsNullOrWhiteSpace(folder) ? string.Empty : Path.GetFullPath(folder),
                    FileNamePattern = parseResult.GetValue(namePattern) ?? ImageBatchService.GetDefaultFileNamePattern(operation)
                };
            }
            catch (ArgumentException ex)
            {
                return JsonStdout.WriteFailureAndExit(CliErrorCodes.Usage, ex.Message);
            }

            var outputs = new List<ImageOutput>();
            var failures = new List<ImageFailure>();
            foreach (string file in parseResult.GetValue(files) ?? [])
            {
                string source = Path.GetFullPath(file);
                try
                {
                    if (!File.Exists(source))
                    {
                        throw new FileNotFoundException("File not found.", source);
                    }

                    outputs.Add(new ImageOutput { Source = source, Output = ImageBatchService.ProcessFile(source, options) });
                }
                catch (Exception ex)
                {
                    failures.Add(new ImageFailure { Source = source, Error = ex.Message });
                }
            }

            JsonStdout.Write(new ImageBatchResponse { Ok = failures.Count == 0, Outputs = outputs.ToArray(), Failures = failures.ToArray() });
            if (!JsonStdout.Enabled)
            {
                Console.Error.WriteLine($"Wrote {outputs.Count} image(s){(failures.Count > 0 ? $", {failures.Count} failed" : string.Empty)}.");
            }

            return failures.Count == 0 ? 0 : 1;
        });
        return command;
    }
}

internal sealed class ImageOutput
{
    public string Source { get; init; } = string.Empty;
    public string Output { get; init; } = string.Empty;
}

internal sealed class ImageFailure
{
    public string Source { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
}

internal sealed class ImageBatchResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; }
    public ImageOutput[] Outputs { get; init; } = [];
    public ImageFailure[] Failures { get; init; } = [];
}
