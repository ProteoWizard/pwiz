/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The NVIDIA driver and the first CUDA device, the one TorchSharp's CUDA device means, asked of the
    /// driver library itself, so an application can tell whether a CUDA build of libtorch would be any
    /// use before it downloads one.
    /// </summary>
    public class CudaDriver
    {
        /// <summary>
        /// The oldest driver libtorch's CUDA 12.8 build runs on: the CUDA 12.8 driver (release 570).
        /// </summary>
        public const int MIN_LIBTORCH_DRIVER_VERSION = 12080;

        /// <summary>
        /// The oldest GPU architecture libtorch's CUDA 12.8 build has kernels for: Volta.
        /// </summary>
        public const int MIN_LIBTORCH_COMPUTE_CAPABILITY = 70;

        private const int CUDA_SUCCESS = 0;
        private const int CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR = 75;
        private const int CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR = 76;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CuInit(uint flags);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CuGetInt(out int value);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CuDeviceGet(out int device, int ordinal);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CuDeviceGetAttribute(out int value, int attribute, int device);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CuDeviceGetName([Out] byte[] name, int length, int device);

        /// <summary>
        /// The driver of this computer, or null when it has no NVIDIA driver or no CUDA device.
        /// </summary>
        public static CudaDriver Find()
        {
            string libraryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"nvcuda.dll" : @"libcuda.so.1";
            if (!NativeLibrary.TryLoad(libraryName, out var library))
                return null;
            // The library stays loaded: libtorch loads it anyway, and the CUDA driver does not support being unloaded
            try
            {
                if (GetFunction<CuInit>(library, @"cuInit")(0) != CUDA_SUCCESS ||
                    GetFunction<CuGetInt>(library, @"cuDriverGetVersion")(out int version) != CUDA_SUCCESS ||
                    GetFunction<CuDeviceGet>(library, @"cuDeviceGet")(out int device, 0) != CUDA_SUCCESS)
                {
                    return null;
                }
                var getAttribute = GetFunction<CuDeviceGetAttribute>(library, @"cuDeviceGetAttribute");
                if (getAttribute(out int major, CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR, device) != CUDA_SUCCESS ||
                    getAttribute(out int minor, CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR, device) != CUDA_SUCCESS)
                {
                    return null;
                }
                var nameBytes = new byte[256];
                string name = null;
                if (GetFunction<CuDeviceGetName>(library, @"cuDeviceGetName")(nameBytes, nameBytes.Length, device) == CUDA_SUCCESS)
                {
                    int length = Array.IndexOf(nameBytes, (byte)0);
                    name = Encoding.ASCII.GetString(nameBytes, 0, length < 0 ? nameBytes.Length : length);
                }
                return new CudaDriver(version, major * 10 + minor, name);
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
        }

        public CudaDriver(int version, int computeCapability, string deviceName)
        {
            Version = version;
            ComputeCapability = computeCapability;
            DeviceName = deviceName;
        }

        /// <summary>
        /// The newest CUDA version the driver supports, as 1000 * major + 10 * minor (12080 for 12.8).
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// The first device's compute capability, as 10 * major + minor (86 for 8.6).
        /// </summary>
        public int ComputeCapability { get; }

        /// <summary>
        /// The first device's name, such as "NVIDIA GeForce RTX 4090", or null if the driver did not give it.
        /// </summary>
        public string DeviceName { get; }

        public bool IsDriverNewEnough => Version >= MIN_LIBTORCH_DRIVER_VERSION;

        public bool IsDeviceNewEnough => ComputeCapability >= MIN_LIBTORCH_COMPUTE_CAPABILITY;

        public bool CanRunLibTorch => IsDriverNewEnough && IsDeviceNewEnough;

        private static T GetFunction<T>(IntPtr library, string name) where T : Delegate
        {
            return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
        }
    }
}
