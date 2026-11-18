using System;

namespace OccasionKit
{
    /// <summary>
    /// The March equinox (the exact Nowruz moment, "Sal Tahvil"), from Meeus, Astronomical Algorithms, ch. 27.
    /// Accurate to about a minute for years 1000 to 3000.
    /// </summary>
    public static class Equinox
    {
        private static readonly DateTime J2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        private const double Deg = Math.PI / 180.0;

        // Table 27.C: A, B (degrees), C (degrees per Julian century).
        private static readonly double[] Terms =
        {
            485, 324.96, 1934.136,
            203, 337.23, 32964.467,
            199, 342.08, 20.186,
            182, 27.85, 445267.112,
            156, 73.14, 45036.886,
            136, 171.52, 22518.443,
            77, 222.54, 65928.934,
            74, 296.72, 3034.906,
            70, 243.58, 9037.513,
            58, 119.81, 33718.147,
            52, 297.17, 150.678,
            50, 21.02, 2281.226,
            45, 247.54, 29929.562,
            44, 325.15, 31555.956,
            29, 60.93, 4443.417,
            18, 155.12, 67555.328,
            17, 288.79, 4562.452,
            16, 198.04, 62894.029,
            14, 199.76, 31436.921,
            12, 95.39, 14577.848,
            12, 287.11, 31931.756,
            12, 320.81, 34777.259,
            9, 227.73, 1222.114,
            8, 15.45, 16859.074,
        };

        /// <summary>The March equinox of a Gregorian year, in UTC.</summary>
        public static DateTime MarchUtc(int year)
        {
            var y = (year - 2000) / 1000.0;
            var jde0 = 2451623.80984 + 365242.37404 * y + 0.05169 * y * y - 0.00411 * y * y * y - 0.00057 * y * y * y * y;
            var t = (jde0 - 2451545.0) / 36525.0;
            var w = (35999.373 * t - 2.47) * Deg;
            var deltaLambda = 1 + 0.0334 * Math.Cos(w) + 0.0007 * Math.Cos(2 * w);

            var s = 0.0;

            for (var i = 0; i < Terms.Length; i += 3)
                s += Terms[i] * Math.Cos((Terms[i + 1] + Terms[i + 2] * t) * Deg);

            var jde = jde0 + 0.00001 * s / deltaLambda;

            // JDE is Terrestrial Time; subtract delta T to get UTC.
            return J2000.AddDays(jde - 2451545.0).AddSeconds(-DeltaTSeconds(year));
        }

        /// <summary>The March equinox in device local time.</summary>
        public static DateTime MarchLocal(int year) => MarchUtc(year).ToLocalTime();

        /// <summary>The March equinox (local time) closest to <paramref name="time"/>.</summary>
        public static DateTime NearestMarchLocal(DateTime time)
        {
            var best = MarchLocal(time.Year);

            foreach (var year in new[] { time.Year - 1, time.Year + 1 })
            {
                if (year < 1001 || year > 2999)
                    continue;

                var candidate = MarchLocal(year);

                if (Math.Abs((candidate - time).Ticks) < Math.Abs((best - time).Ticks))
                    best = candidate;
            }

            return best;
        }

        // Espenak & Meeus polynomial fits.
        private static double DeltaTSeconds(int year)
        {
            if (year < 2005)
                return 64.0;

            if (year <= 2050)
            {
                double t = year - 2000;
                return 62.92 + 0.32217 * t + 0.005589 * t * t;
            }

            var u = (year - 1820) / 100.0;
            return -20 + 32 * u * u - 0.5628 * (2150 - year);
        }
    }
}
