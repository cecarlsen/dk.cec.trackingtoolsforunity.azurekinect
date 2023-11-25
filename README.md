# TrackingTools.KinectAzure

A set of tools for Unity to make Kinect Azure and video projector calibration easier.

### Dependencies

- TrackingTools - MIT licensed.
- [OpenCV for Unity](https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088) - Sold on the Unity Asset Store.
- [Azure Kinect Examples for Unity](https://assetstore.unity.com/packages/tools/integration/azure-kinect-examples-for-unity-149700) - Sold on the Unity Asset Store.

### Installation

1) Import OpenCV for Unity from the Asset Store.
1) Import Azure Kinect Examples for Unity from the Asset Store.
1) Import Tracking Tools for Unity from Github.

You will see red error because of missing references.
1) Add an Assembly Definition to the AzureKinectExamples folder and add a refernece to that in the TrackingToolsAzureKinect Assembly Definition located in the package/Runtime folder of this package. 
2) You will still see errors related to KinectManager.GetSensorData(). To fix that, edit the GetSensorData method from being internal to being public.


### Author
Carl Emil Carlsen | [cec.dk](http://cec.dk) | [github](https://github.com/cecarlsen)