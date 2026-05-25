import React, { useEffect, useRef, useState } from 'react';
import './WeekHorizontal.css';
import { Schedule, WeekSchedule } from '../Schedule';
import { Tooltip } from "antd";
import ModalBookingDetails from './ModalBookingDetails';

interface WeekHorizontalProps {
    weekSchedule: WeekSchedule;
    onDeleteSchedule: (schedule: Schedule) => Promise<string | null>;
    onConfirmSchedule: (schedule: Schedule) => Promise<string | null>;
}

const WeekHorizontal: React.FC<WeekHorizontalProps> = ({ weekSchedule, onDeleteSchedule, onConfirmSchedule }) => {
    const startDate = weekSchedule.startDate;
    const [columnOffsets, setColumnOffsets] = useState<number[]>([]);
    const tableRef = useRef<HTMLTableElement>(null);

    const measureColumnOffsets = () => {
        if (tableRef.current) {
            const hourCells = Array.from(tableRef.current.querySelectorAll('.hour-header')) as HTMLElement[];
            const offsets = hourCells.map((cell) => cell.offsetLeft);
            setColumnOffsets(offsets);
        }
    };

    useEffect(() => {
        measureColumnOffsets();

        const handleResize = () => {
            measureColumnOffsets();
        };

        window.addEventListener('resize', handleResize);
        return () => {
            window.removeEventListener('resize', handleResize);
        };
    }, []);

    const days = Array.from({ length: 7 }, (_, i) => {
        const date = new Date(startDate);
        date.setDate(startDate.getDate() + i);

        const dayOfWeek = new Intl.DateTimeFormat('vi-VN', { weekday: 'long' }).format(date);
        const dayAndMonth = `${date.getDate()}/${date.getMonth() + 1}`;
        const dayName = (
            <>
                {dayOfWeek}
                <br />
                ({dayAndMonth})
            </>
        );

        return {
            key: date.toISOString(),
            dayName,
            date,
        };
    });

    const hours = Array.from({ length: 14 }, (_, i) => 7 + i);

    const [isModalOpen, setIsModalOpen] = useState(false);
    const [selectedSchedule, setSelectedSchedule] = useState<Schedule | null>(null);
    const firstOffset = columnOffsets[0] ?? 0;
    const normalizedOffsets = columnOffsets.map((offset) => offset - firstOffset);
    const defaultHourWidth = normalizedOffsets.length > 1 ? normalizedOffsets[1] - normalizedOffsets[0] : 0;
    const getHourWidth = (hourIndex: number): number => {
        if (normalizedOffsets.length <= 1) {
            return defaultHourWidth;
        }

        if (hourIndex < normalizedOffsets.length - 1) {
            return normalizedOffsets[hourIndex + 1] - normalizedOffsets[hourIndex];
        }

        return normalizedOffsets[hourIndex] - normalizedOffsets[hourIndex - 1];
    };
    const getOffsetAtMinutes = (minutesSinceSeven: number): number => {
        if (normalizedOffsets.length === 0) {
            return 0;
        }

        const clampedMinutes = Math.max(0, Math.min(minutesSinceSeven, hours.length * 60));
        if (clampedMinutes === hours.length * 60) {
            const lastIndex = hours.length - 1;
            return normalizedOffsets[lastIndex] + getHourWidth(lastIndex);
        }

        const hourIndex = Math.min(Math.floor(clampedMinutes / 60), hours.length - 1);
        const minuteInHour = clampedMinutes % 60;
        return normalizedOffsets[hourIndex] + (minuteInHour / 60) * getHourWidth(hourIndex);
    };

    return (
        <div className="week-horizontal-table">
            <ModalBookingDetails
                isModalOpen={isModalOpen}
                setIsModalOpen={setIsModalOpen}
                selectedSchedule={selectedSchedule}
                handleDeleteSchedule={onDeleteSchedule}
                handleConfirmSchedule={onConfirmSchedule}
            />

            <table className="custom-table" ref={tableRef}>
                <thead>
                    <tr>
                        <th></th>
                        {hours.map((hour) => (
                            <th key={hour} className="hour-header">
                                <span className="hour-label">{`${hour}:00`}</span>
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {
                        days.map((day) => {
                            const schedulesForDay = weekSchedule.schedules.filter(
                                (schedule) => schedule.Date.toDateString() === day.date.toDateString()
                            );

                            const isToday = new Date().toDateString() === day.date.toDateString();

                            return (
                                <tr key={day.key} className={isToday ? 'today-row' : ''}>
                                    <td className="day-cell">{day.dayName}</td>
                                    {hours.map((hour, index) => {
                                        if (index === 0) {
                                            // The first column (7 AM) contains the schedule container
                                            return (
                                                <td key={hour} className="schedule-cell" style={{ position: 'relative' }}>
                                                    {schedulesForDay.map((schedule) => {
                                                        const [startHour, startMinute] = schedule.StartTime.split(':').map(Number);
                                                        const [endHour, endMinute] = schedule.EndTime.split(':').map(Number);

                                                        const startTotalMinutes = (startHour - 7) * 60 + startMinute;
                                                        const endTotalMinutes = (endHour - 7) * 60 + endMinute;
                                                        const startOffset = getOffsetAtMinutes(startTotalMinutes);
                                                        const endOffset = getOffsetAtMinutes(endTotalMinutes);
                                                        const spanWidth = Math.max(endOffset - startOffset, 0);

                                                        return (
                                                            <Tooltip title={`${schedule.Title} (phòng R${schedule.Room}, do ${schedule.Creator} đặt) - ${schedule.StartTime} - ${schedule.EndTime}`} key={schedule.Id}>
                                                                <div
                                                                    className={`schedule-item room-${schedule.Room} state-${schedule.State}`}
                                                                    style={{
                                                                        position: 'absolute',
                                                                        left: `${startOffset}px`,
                                                                        width: `${spanWidth}px`,
                                                                    }}
                                                                    onClick={() => {
                                                                        setSelectedSchedule(schedule);
                                                                        setIsModalOpen(true);
                                                                    }}
                                                                >
                                                                    {schedule.Title}
                                                                </div>
                                                            </Tooltip>
                                                        );
                                                    })}
                                                </td>
                                            );
                                        }
                                        // Empty cells for other hours
                                        return <td key={hour}></td>;
                                    })}
                                </tr>
                            );
                        })}
                </tbody>
            </table>
        </div >
    );
};

export default WeekHorizontal;
