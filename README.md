# TrackingTools.KinectAzure

A set of tools for Unity to make Kinect Azure and video projector calibration easier.

### Dependencies

- Unity 2023.1 (it may work with other versions, but no promises).
- [TrackingToolsForUnity](https://github.com/cecarlsen/dk.cec.trackingtoolsforunity) (MIT licensed) and it's dependencies.
- [Azure Kinect Examples for Unity](https://assetstore.unity.com/packages/tools/integration/azure-kinect-examples-for-unity-149700) (Sold on the Unity Asset Store).

### Installation

1) Import OpenCV for Unity from the Asset Store.
1) Import Azure Kinect Examples for Unity from the Asset Store.
1) Download [dk.cec.trackingtoolsforunity](https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088), place it in your Packages folder and make sure the name is exactly "dk.cec.trackingtoolsforunity".

Back in unity you will see a bunch of red errors because of missing references.
1) Add an Assembly Definition to the AzureKinectExamples folder and add a refernece to that in the TrackingToolsAzureKinect Assembly Definition located in the package/Runtime folder of this package. 
2) You will still see errors related to KinectManager.GetSensorData(). To fix that, edit the GetSensorData method from being internal to being public.

## MonoBehaviours

#### KinectAzureTextureProvider
Creates and forwards a IR, Depth and Color RenderTextures from the Kinect Azure with optional undistortion and flip.

#### KinectAzureTexture2DProvider  
Creates and forwards a IR and Colour Texture2D from the Kinect Azure.

#### KinectAzureIntrinsicsExtractor  
Extracts and saves the Azure Kinect intrinsics as to a json file. 

## Credits

### Author
Carl Emil Carlsen | [cec.dk](http://cec.dk) | [github](https://github.com/cecarlsen)