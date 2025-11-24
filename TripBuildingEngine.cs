private async Task<TripEntity> CreateNewTrip(
    Booking booking,
    CreateTripCommand createTripCommand,
    List<CreateBookingMetaCommand> additionalBookingMeta)
{
    await _mediatorSender.Send(new UpdateBookingCommand
    {
        BookingId = booking.Id,
        BookingMetas = additionalBookingMeta
    });

    // This will handle the trip creation
    var createdTrip = await _mediatorSender.Send(createTripCommand);

    return createdTrip;
}

// See UpdateBookingCommand file