﻿using System;
using System.Linq;
using System.Reactive.Linq;
using OpenCV.Net;
using TensorFlow;
using System.ComponentModel;

namespace Bonsai.DeepLabCut
{
    [DefaultProperty(nameof(ModelFileName))]
    [Description("Performs markerless pose estimation using a DeepLabCut model on the input image sequence.")]
    public class DetectPose : Transform<IplImage, Pose>
    {
        [FileNameFilter("Protocol Buffer Files(*.pb)|*.pb")]
        [Editor("Bonsai.Design.OpenFileNameEditor, Bonsai.Design", DesignTypes.UITypeEditor)]
        [Description("The path to the exported Protocol Buffer file containing the pretrained DeepLabCut model.")]
        public string ModelFileName { get; set; }

        [FileNameFilter("YAML Files(*.yaml)|*.yaml|All Files|*.*")]
        [Editor("Bonsai.Design.OpenFileNameEditor, Bonsai.Design", DesignTypes.UITypeEditor)]
        [Description("The path to the configuration YAML file containing joint labels.")]
        public string PoseConfigFileName { get; set; }

        [Range(0, 1)]
        [Editor(DesignTypes.SliderEditor, DesignTypes.UITypeEditor)]
        [Description("The optional confidence threshold used to discard position values.")]
        public float? MinConfidence { get; set; }

        [Description("The optional scale factor used to resize video frames for inference.")]
        public float? ScaleFactor { get; set; }

        public override IObservable<Pose> Process(IObservable<IplImage> source)
        {
            return Observable.Defer(() =>
            {
                IplImage temp = null;
                TFTensor tensor = null;
                TFSession.Runner runner = null;
                var graph = TensorHelper.ImportModel(ModelFileName, out TFSession session);
                var config = ConfigHelper.PoseConfig(PoseConfigFileName);
                return source.Select(input =>
                {
                    var poseScale = 1.0;
                    var tensorSize = input.Size;
                    var scaleFactor = ScaleFactor;
                    if (scaleFactor.HasValue)
                    {
                        poseScale = scaleFactor.Value;
                        tensorSize.Width = (int)(tensorSize.Width * poseScale);
                        tensorSize.Height = (int)(tensorSize.Height * poseScale);
                        poseScale = 1.0 / poseScale;
                    }

                    if (tensor == null || tensor.GetTensorDimension(1) != tensorSize.Height || tensor.GetTensorDimension(2) != tensorSize.Width)
                    {
                        runner = session.GetRunner();
                        tensor = TensorHelper.CreatePlaceholder(graph, runner, tensorSize);
                        runner.Fetch("concat_1");
                    }

                    // Run the model
                    TensorHelper.UpdateTensor(tensor, tensorSize, input, ref temp);
                    var output = runner.Run();

                    // Fetch the results from output
                    var poseTensor = output[0];
                    var pose = new Mat((int)poseTensor.Shape[0], (int)poseTensor.Shape[1], Depth.F32, 1, poseTensor.Data);
                    var result = new Pose(input);
                    var threshold = MinConfidence;
                    for (int i = 0; i < pose.Rows; i++)
                    {
                        BodyPart bodyPart;
                        bodyPart.Name = config[i];
                        bodyPart.Confidence = (float)pose.GetReal(i, 2);
                        if (bodyPart.Confidence < threshold)
                        {
                            bodyPart.Position = new Point2f(float.NaN, float.NaN);
                        }
                        else
                        {
                            bodyPart.Position.X = (float)(pose.GetReal(i, 1) * poseScale);
                            bodyPart.Position.Y = (float)(pose.GetReal(i, 0) * poseScale);
                        }
                        result.Add(bodyPart);
                    }
                    return result;
                });
            });
        }
    }
}
