using System;

namespace TableauCri.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Get Url comprised of specified parameters
        /// </summary>
        /// <param name="str"></param>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static string AppendUri(this string str, string uri)
        {
            if (String.IsNullOrEmpty(str))
            {
                return uri;
            }

            if (String.IsNullOrEmpty(uri))
            {
                return str;
            }

            return String.Format("{0}/{1}", str.TrimEnd('/', '\\'), uri.TrimStart('/', '\\'));
        }

        /// <summary>
        /// Perform case insensitive equality check with specified value
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool EqualsIgnoreCase(this string str, string value)
        {
            return str.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
    }
}