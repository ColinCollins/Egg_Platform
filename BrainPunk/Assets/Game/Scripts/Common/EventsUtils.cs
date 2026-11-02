using Bear.EventSystem;
using UnityEngine;

public static class EventsUtils
{
    public static void ResetEvents(ref EventSubscriber subscriber)
    {
        if (subscriber == null)
            subscriber = new EventSubscriber();

        subscriber.Dispose();
    }
}
