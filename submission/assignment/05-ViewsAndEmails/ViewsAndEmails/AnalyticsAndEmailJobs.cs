namespace ViewsAndEmails;

public sealed class AnalyticsAndEmailJobs
{
    public readonly Dictionary<string, int> Views = new();
    public readonly Queue<string> EmailJobs = new();
    public readonly List<string> SentEmails = new();

    readonly object _viewsSync = new();
    readonly object _jobsSync = new();
    readonly object _sentSync = new();
    public volatile bool ProducersDone;

    public void RecordView(string product)
    {
        lock (_viewsSync)
        {
            Views.TryGetValue(product, out int current);
            Views[product] = current + 1;
        }
    }

    public void PlaceOrder(int orderId)
    {
        lock (_jobsSync)
            EmailJobs.Enqueue($"Email: order {orderId}");
    }

    public void EmailWorker()
    {
        while (true)
        {
            bool done = ProducersDone;      // اقرا الأول

            string? job;
            bool hasJob;
            lock (_jobsSync)
                hasJob = EmailJobs.TryDequeue(out job);

            if (hasJob)
            {
                lock (_sentSync)
                    SentEmails.Add(job!);
                continue;
            }

            if (done)                       // خلصوا قبل ما أفحص، والطابور فاضي = خلاص
                return;

            Thread.Sleep(1);
        }
    }
}