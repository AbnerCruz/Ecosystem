namespace Urbe.Client;

/// <summary>Reading position is user-owned. New output follows only until the person deliberately scrolls away.</summary>
public static class ChatViewportPolicy
{
    public static bool FollowingAfterGesture(bool wasFollowing, bool movedByReader, bool atBottom) =>
        movedByReader ? atBottom : wasFollowing;

    public static bool MayAutoFollow(bool followingTail, bool textSelectionActive) => followingTail && !textSelectionActive;
}
