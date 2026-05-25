namespace RoomBookingBackend.Utilities;

public static class BookingResults
{
    // Booking added successfully
    public const string BookingAddSuccess = nameof(BookingAddSuccess);

    // Booking add failure: timeslot overlapped
    public const string BookingAddFailureOverlap = nameof(BookingAddFailureOverlap);

    // Booking add failure: invalid date
    public const string BookingAddFailureInvalidDate = nameof(BookingAddFailureInvalidDate);

    // Booking add failure: invalid time
    public const string BookingAddFailureInvalidTime = nameof(BookingAddFailureInvalidTime);

    // Booking add failure: invalid room
    public const string BookingAddFailureInvalidRoom = nameof(BookingAddFailureInvalidRoom);

    // Booking add failure: invalid state (trying to set state Booked when room is 1 and in awaiting confirmation)
    public const string BookingAddFailureInvalidState = nameof(BookingAddFailureInvalidState);

    // Booking add failure: unauthorized
    public const string BookingAddFailureUnauthorized = nameof(BookingAddFailureUnauthorized);

    // Booking delete success
    public const string BookingDeleteSuccess = nameof(BookingDeleteSuccess);

    // Booking delete failure: trying to delete another user's booking
    public const string BookingDeleteNotAllowed = nameof(BookingDeleteNotAllowed);

    // Booking delete failure: booking not found
    public const string BookingDeleteNotFound = nameof(BookingDeleteNotFound);
}
