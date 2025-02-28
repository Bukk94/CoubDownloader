namespace CoubDownloader.Configurations
{
    public class Pager
    {
        public int Take { get; set; } = -1;

        public int Current { get; private set; }

        public bool HasNext => Take == -1 || Current < Take;

        public void Use()
        {
            Current++;
        }
    }
}