using MediatR;
using Zii.Trip.Common.Application.Commands.CreateBooking;
using Zii.Trip.Common.Application.Common.Interfaces;
using Zii.Trip.Common.Application.Mappings;
using Zii.Trip.Common.Domain.Entities;

namespace Zii.Trip.Common.Application.Commands.UpdateBooking;

public record UpdateBookingCommand : IRequest<Booking>
{
    public Guid BookingId { get; set; }

    public virtual IEnumerable<CreateBookingMetaCommand> BookingMetas { get; set; } =
        new List<CreateBookingMetaCommand>();
}

public class UpdateBookingCommandHandler : IRequestHandler<UpdateBookingCommand, Booking>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITripRepository _tripRepository;

    public UpdateBookingCommandHandler(IUnitOfWork unitOfWork, ITripRepository tripRepository)
    {
        _unitOfWork = unitOfWork;
        _tripRepository = tripRepository;
    }

    public async Task<Booking> Handle(UpdateBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await _tripRepository.GetBookingWithIncludeMetasByIdAsync(request.BookingId).ConfigureAwait(false)
                      ?? throw new Exception("Invalid booking id");
        request.BookingMetas.ToList().ForEach(m =>
        {
            booking.Metas.RemoveAll(metaInDb => metaInDb.MetaKey == m.MetaKey);
            booking.Metas.Add(m.ToBookingMeta());
        });

        _unitOfWork.BookingRepository.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return booking;
    }
}