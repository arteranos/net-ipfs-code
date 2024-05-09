using Ipfs.ExtendedApi;

namespace Ipfs.Http
{
    public partial class IpfsClientEx : IpfsClient
    {
        public IpfsClientEx() : base() 
        { 
            InitEx();
        }

        public IpfsClientEx(string host) : base(host) 
        { 
            InitEx();
        }

        private void InitEx() 
        {
            Routing = new RoutingApi(this);
            NameEx = new NameExApi(this);
        }

        public IRoutingApi Routing { get; private set; }

        public INameExApi NameEx { get; private set; }

    }
}