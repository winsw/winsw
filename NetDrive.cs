using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace winsw
{
        /// <summary>
        /// Specify the drive mapping activities prior to the launch.
        /// </summary>
    public class NetDrive
    {
        public readonly string localDrive;
        public readonly string shareName;

        internal NetDrive(string drive, string share)
        {
            localDrive = drive;
            shareName = share;

            if (localDrive == null)
            {
                throw new ArgumentNullException("localDrive", "NetDrive creation requires a local drive.");
            }

            if (shareName == null)
            {
                throw new ArgumentNullException("shareName", "Netdrive creation requires a share name.");
            }
        }

        public void MapDrive()
        {
            aejw.cNetworkDrive netDrive = new aejw.cNetworkDrive();

            netDrive.LocalDrive = localDrive;
            netDrive.ShareName = shareName;
            netDrive.MapDrive();
        }

        public void UnMapDrive()
        {
            aejw.cNetworkDrive netDrive = new aejw.cNetworkDrive();

            netDrive.LocalDrive = localDrive;
            netDrive.UnMapDrive();
        }
    }
}
