using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace SDRInterface;

/// <summary>
    /// Utility functions to generate Pothosware.SoapySDR.Device parameters
    /// </summary>
    public class Utility
    {
        //
        // Public
        //

        /// <summary>
        /// Return the SoapySDR complex format string that corresponds to the given type.
        /// This string will be passed into Device's stream setup functions.
        ///
        /// This function will throw if the type is unsupported.
        /// </summary>
        /// <typeparam name="T">The format type</typeparam>
        /// <returns>The type's complex format string (see Pothosware.SoapySDR.StreamFormat)</returns>
        public static string GetFormatString<T>() where T : unmanaged
        {
            var type = typeof(T);

            if (typeof(T).Equals(typeof(sbyte))) return StreamFormat.ComplexInt8;
            else if (typeof(T).Equals(typeof(short))) return StreamFormat.ComplexInt16;
            else if (typeof(T).Equals(typeof(int))) return StreamFormat.ComplexInt32;
            else if (typeof(T).Equals(typeof(byte))) return StreamFormat.ComplexUInt8;
            else if (typeof(T).Equals(typeof(ushort))) return StreamFormat.ComplexUInt16;
            else if (typeof(T).Equals(typeof(uint))) return StreamFormat.ComplexUInt32;
            else if (typeof(T).Equals(typeof(float))) return StreamFormat.ComplexFloat32;
            else if (typeof(T).Equals(typeof(double))) return StreamFormat.ComplexFloat64;
            else throw new Exception(string.Format("Type {0} not covered by GetFormatString", type));
        }

        /// <summary>
        /// Convert a markup string to a key-value map.
        ///
        /// The markup format is: "key0=value0, key1=value1".
        /// </summary>
        /// <param name="args">The markup string</param>
        /// <returns>An equivalent key-value map</returns>
        public static IDictionary<string, string> StringToKwargs(string args)
        {
            var kwargs = new Dictionary<string, string>();

            var inKey = true;
            var key = "";
            var val = "";
            for (var i = 0; i < args.Length; i++)
            {
                var ch = args[i];
                if (inKey)
                {
                    if (ch == '=') inKey = false;
                    else if (ch == ',') inKey = true;
                    else key += ch;
                }
                else
                {
                    if (ch == ',') inKey = true;
                    else val += ch;
                }

                if (inKey && (!string.IsNullOrEmpty(val) || ch == ',') || i == args.Length - 1)
                {
                    key = key.Trim();
                    val = val.Trim();
                    if (!string.IsNullOrEmpty(key)) kwargs[key] = val;
                    key = "";
                    val = "";
                }
            }

            return kwargs;
        }

        /// <summary>
        /// Convert a key-value map to a markup string.
        ///
        /// The markup format is: "key0=value0, key1=value1".
        /// </summary>
        /// <param name="kwargs">The key-value map</param>
        /// <returns>An equivalent markup string</returns>
        public static string KwargsToString(IDictionary<string, string> kwargs)
        {
            var builder = new StringBuilder();

            foreach (var pair in kwargs)
            {
                if (builder.Length > 0) builder.Append(", ");
                builder.Append(pair.Key).Append('=').Append(pair.Value);
            }

            return builder.ToString();
        }


        internal unsafe static void*[] ToPointerListInternal<T>(
            Memory<T>[] memory,
            out MemoryHandle[] memoryHandles)
        {
            memoryHandles = memory.Select(mem => mem.Pin()).ToArray();
            return ToPointerListInternal(memoryHandles.Select(handle => (IntPtr)handle.Pointer).ToArray());
        }

        internal unsafe static void*[] ToPointerListInternal<T>(
            ReadOnlyMemory<T>[] memory,
            out MemoryHandle[] memoryHandles)
        {
            memoryHandles = memory.Select(mem => mem.Pin()).ToArray();
            return ToPointerListInternal(memoryHandles.Select(handle => (IntPtr)handle.Pointer).ToArray());
        }

        internal static unsafe void*[] ToPointerListInternal(IntPtr[] arr)
        {
            var ret = new void*[arr.Length];
            for (var i = 0; i < arr.Length; i++)
                ret[i] = (void*) arr[i];
            return ret;
        }
    }