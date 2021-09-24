using System;
using System.Runtime.InteropServices;
using UnityEditor;

// only support x64 windows because dll is x64
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && UNITY_64

// custum C# binding of the oidn header files

namespace oidn
{
    // -----------------------------------------------------------------------------
    // Device
    // -----------------------------------------------------------------------------

    // Device types
    public enum OIDNDeviceType
    {
        OIDN_DEVICE_TYPE_DEFAULT = 0, // select device automatically
        OIDN_DEVICE_TYPE_CPU = 1 // CPU device
    }

    // Error codes
    public enum OIDNError
    {
        OIDN_ERROR_NONE = 0, // no error occurred
        OIDN_ERROR_UNKNOWN = 1, // an unknown error occurred
        OIDN_ERROR_INVALID_ARGUMENT = 2, // an invalid argument was specified
        OIDN_ERROR_INVALID_OPERATION = 3, // the operation is not allowed
        OIDN_ERROR_OUT_OF_MEMORY = 4, // not enough memory to execute the operation
        OIDN_ERROR_UNSUPPORTED_HARDWARE = 5, // the hardware (e.g. CPU) is not supported
        OIDN_ERROR_CANCELLED = 6, // the operation was cancelled by the user
    }

    // -----------------------------------------------------------------------------
    // Buffer
    // -----------------------------------------------------------------------------

    // Formats for images and other data stored in buffers
    public enum OIDNFormat
    {
        OIDN_FORMAT_UNDEFINED = 0,
        // 32-bit single-precision floating point scalar and vector formats
        OIDN_FORMAT_FLOAT = 1,
        OIDN_FORMAT_FLOAT2 = 2,
        OIDN_FORMAT_FLOAT3 = 3,
        OIDN_FORMAT_FLOAT4 = 4,
    }

    // Access modes for mapping buffers
    public enum OIDNAccess
    {
        OIDN_ACCESS_READ = 0, // read-only access
        OIDN_ACCESS_WRITE = 1, // write-only access
        OIDN_ACCESS_READ_WRITE = 2, // read and write access
        OIDN_ACCESS_WRITE_DISCARD = 3, // write-only access, previous contents discarded
    }

    public class OIDN_API
    {
        // -----------------------------------------------------------------------------
        // Config
        // -----------------------------------------------------------------------------

        public static int OIDN_VERSION_MAJOR = 1;
        public static int OIDN_VERSION_MINOR = 4;
        public static int OIDN_VERSION_PATCH = 1;
        public static int OIDN_VERSION = 10401;
        public static string OIDN_VERSION_STRING = "1.4.1";

        // -----------------------------------------------------------------------------
        // Device
        // -----------------------------------------------------------------------------

        // Creates a new device.
        [DllImport("OpenImageDenoise.dll")]
        public static extern IntPtr oidnNewDevice(OIDNDeviceType type);

        // Retains the device (increments the reference count).
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnRetainDevice(IntPtr device);

        // Releases the device (decrements the reference count).
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnReleaseDevice(IntPtr device);

        // Sets a boolean parameter of the device.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetDevice1b(IntPtr device, string name, bool value);

        // Sets an integer parameter of the device.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetDevice1i(IntPtr device, string name, int value);

        // Gets a boolean parameter of the device.
        [DllImport("OpenImageDenoise.dll")]
        public static extern bool oidnGetDevice1b(IntPtr device, string name);

        // Gets an integer parameter of the device (e.g. "version").
        [DllImport("OpenImageDenoise.dll")]
        public static extern int oidnGetDevice1i(IntPtr device, string name);

        // Sets the error callback function of the device.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetDeviceErrorFunction(IntPtr device, Func<IntPtr,OIDNError,string> func, IntPtr userPtr);

        // Returns the first unqueried error code stored in the device for the current
        // thread, optionally also returning a string message (if not NULL), and clears
        // the stored error. Can be called with a NULL device as well to check why a
        // device creation failed.
        [DllImport("OpenImageDenoise.dll")]
        public static extern OIDNError oidnGetDeviceError(IntPtr device, out string outMessage);

        // Commits all previous changes to the device.
        // Must be called before first using the device (e.g. creating filters).
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnCommitDevice(IntPtr device);

        // -----------------------------------------------------------------------------
        // Buffer
        // -----------------------------------------------------------------------------

        // Creates a new buffer (data allocated and owned by the device).
        [DllImport("OpenImageDenoise.dll")]
        public static extern IntPtr oidnNewBuffer(IntPtr device, uint byteSize);

        // Creates a new shared buffer (data allocated and owned by the user).
        [DllImport("OpenImageDenoise.dll")]
        public static extern IntPtr oidnNewSharedBuffer(IntPtr device, IntPtr ptr, uint byteSize);

        // Maps a region of the buffer to host memory.
        // If byteSize is 0, the maximum available amount of memory will be mapped.
        [DllImport("OpenImageDenoise.dll")]
        public static extern IntPtr oidnMapBuffer(IntPtr buffer, OIDNAccess access, uint byteOffset, uint byteSize);

        // Unmaps a region of the buffer.
        // mappedPtr must be a pointer returned by a previous call to oidnMapBuffer.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnUnmapBuffer(IntPtr buffer, IntPtr mappedPtr);

        // Retains the buffer (increments the reference count).
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnRetainBuffer(IntPtr buffer);

        // Releases the buffer (decrements the reference count).
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnReleaseBuffer(IntPtr buffer);

        // -----------------------------------------------------------------------------
        // Filter
        // -----------------------------------------------------------------------------

        // Creates a new filter of the specified type (e.g. "RT").
        [DllImport("OpenImageDenoise.dll")]
        public static extern IntPtr oidnNewFilter(IntPtr device, string type);

        // Retains the filter (increments the reference count).
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnRetainFilter(IntPtr filter);

        // Releases the filter (decrements the reference count).
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnReleaseFilter(IntPtr filter);

        // Sets an image parameter of the filter (stored in a buffer).
        // If bytePixelStride and/or byteRowStride are zero, these will be computed automatically.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetFilterImage(IntPtr filter, string name,
                                         IntPtr buffer, OIDNFormat format,
                                         uint width, uint height,
                                         uint byteOffset,
                                         uint bytePixelStride, uint byteRowStride);

        // Sets an image parameter of the filter (owned by the user).
        // If bytePixelStride and/or byteRowStride are zero, these will be computed automatically.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetSharedFilterImage(IntPtr filter, string name,
                                       IntPtr ptr, OIDNFormat format,
                                       uint width, uint height,
                                       uint byteOffset,
                                       uint bytePixelStride, uint byteRowStride);

        // Removes an image parameter of the filter that was previously set.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnRemoveFilterImage(IntPtr filter, string name);

        // Sets an opaque data parameter of the filter (owned by the user).
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetSharedFilterData(IntPtr filter, string name,
                                      IntPtr ptr, uint byteSize);

        // Notifies the filter that the contents of an opaque data parameter has been changed.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnUpdateFilterData(IntPtr filter, string name);

        // Removes an opaque data parameter of the filter that was previously set.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnRemoveFilterData(IntPtr filter, string name);

        // Sets a boolean parameter of the filter.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetFilter1b(IntPtr filter, string name, bool value);

        // Gets a boolean parameter of the filter.
        [DllImport("OpenImageDenoise.dll")]
        public static extern bool oidnGetFilter1b(IntPtr filter, string name);

        // Sets an integer parameter of the filter.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetFilter1i(IntPtr filter, string name, int value);

        // Gets an integer parameter of the filter.
        [DllImport("OpenImageDenoise.dll")]
        public static extern int oidnGetFilter1i(IntPtr filter, string name);

        // Sets a float parameter of the filter.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetFilter1f(IntPtr filter, string name, float value);

        // Gets a float parameter of the filter.
        [DllImport("OpenImageDenoise.dll")]
        public static extern float oidnGetFilter1f(IntPtr filter, string name);

        // Sets the progress monitor callback function of the filter.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnSetFilterProgressMonitorFunction(IntPtr filter, Func<IntPtr,double> func, IntPtr userPtr);

        // Commits all previous changes to the filter.
        // Must be called before first executing the filter.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnCommitFilter(IntPtr filter);

        // Executes the filter.
        [DllImport("OpenImageDenoise.dll")]
        public static extern void oidnExecuteFilter(IntPtr filter);
    }
}

#endif