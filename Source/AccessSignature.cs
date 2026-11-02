using System;
namespace Pengin.AnimalManagement
{
    // Up to 400 consumers, packed into seven value-type words. This replaces
    // allocating an N-character access string for each food stack.
    public readonly struct AccessSignature : IEquatable<AccessSignature>
    {
        readonly ulong a,b,c,d,e,f,g;
        readonly int expiry;
        AccessSignature(ulong a,ulong b,ulong c,ulong d,ulong e,ulong f,ulong g,int expiry)
        {this.a=a;this.b=b;this.c=c;this.d=d;this.e=e;this.f=f;this.g=g;this.expiry=expiry;}
        public static AccessSignature Create(bool[] access,int expiry)
        {
            if(access==null||access.Length>400)throw new ArgumentException("Invalid consumer access mask");
            ulong a=0,b=0,c=0,d=0,e=0,f=0,g=0;
            for(int i=0;i<access.Length;i++)
            {
                if(!access[i])continue;
                ulong bit=1UL<<(i&63);
                switch(i>>6)
                {
                    case 0:a|=bit;break;case 1:b|=bit;break;
                    case 2:c|=bit;break;case 3:d|=bit;break;
                    case 4:e|=bit;break;case 5:f|=bit;break;
                    case 6:g|=bit;break;
                }
            }
            return new AccessSignature(a,b,c,d,e,f,g,expiry);
        }
        public bool Equals(AccessSignature other)
        {return expiry==other.expiry&&a==other.a&&b==other.b&&c==other.c&&d==other.d&&e==other.e&&f==other.f&&g==other.g;}
        public override bool Equals(object obj){return obj is AccessSignature other&&Equals(other);}
        public override int GetHashCode()
        {
            unchecked
            {
                int hash=expiry;
                hash=hash*31+(int)(a^(a>>32));hash=hash*31+(int)(b^(b>>32));
                hash=hash*31+(int)(c^(c>>32));hash=hash*31+(int)(d^(d>>32));
                hash=hash*31+(int)(e^(e>>32));hash=hash*31+(int)(f^(f>>32));
                return hash*31+(int)(g^(g>>32));
            }
        }
    }
}
