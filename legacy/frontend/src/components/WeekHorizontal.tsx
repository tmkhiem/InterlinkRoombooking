import React, { useEffect, useRef, useState } from 'react';
import './WeekHorizontal.css'; // Add a CSS file for styling
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

    // Function to measure the x-offset of each hour column
    const measureColumnOffsets = () => {
        if (tableRef.current) {
            const hourCells = Array.from(tableRef.current.querySelectorAll('.hour-header')) as HTMLElement[];
            const offsets = hourCells.map((cell) => cell.offsetLeft);
            setColumnOffsets(offsets);
            // console.log('Column offsets:', offsets);
        }
    };

    // Measure the column offsets on mount and on window resize
    useEffect(() => {
        measureColumnOffsets(); // Initial measurement

        const handleResize = () => {
            measureColumnOffsets(); // Recalculate on resize
        };

        window.addEventListener('resize', handleResize); // Add resize listener
        return () => {
            window.removeEventListener('resize', handleResize); // Cleanup listener on unmount
        };
    }, []);

    // Generate 7 consecutive days
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

    // Generate columns for hours from 7 AM to 7 PM
    const hours = Array.from({ length: 14 }, (_, i) => 7 + i);

    const [isModalOpen, setIsModalOpen] = useState(false);
    const [selectedSchedule, setSelectedSchedule] = useState<Schedule | null>(null);

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
                                <div>&nbsp;</div>
                                <div style={{
                                    zIndex: 1,
                                    marginLeft: '-1.5rem',
                                    marginTop: '-1.4rem',
                                    position: 'fixed'                                    
                                }}>
                                    {`${hour}:00`}
                                </div>
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {
                        days.map((day) => {
                            const schedulesForDay = weekSchedule.schedules.filter(
                                (schedule) => {
                                    // console.log('schedule.Date:', schedule.Date, 'day.date:', day.date, 'type: ', typeof schedule.Date, typeof day.date);
                                    schedule.Date.toDateString() === day.date.toDateString();
                                    if (schedule.Date.toDateString() === day.date.toDateString()) {
                                        //console.log(schedule.Date.toDateString(), day.date.toDateString(), schedule.Date.toDateString() === day.date.toDateString())
                                        return true;
                                    }
                                }
                            );

                            // console.log('schedulesForDay: ', day.date, schedulesForDay, 'schedules: ', weekSchedule.schedules);

                            // Check if the current day is today's date in DD/MM/YYYY format                            
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

                                                        // Calculate the total minutes since 7 AM for start and end times
                                                        const startTotalMinutes = (startHour - 7) * 60 + startMinute;
                                                        const endTotalMinutes = (endHour - 7) * 60 + endMinute;
                                                        const totalMinutes = endTotalMinutes - startTotalMinutes;

                                                        // Find the x-offset of the nearest column for start and end times
                                                        const startColumnIndex = Math.floor(startTotalMinutes / 60.0) - 1;
                                                        const endColumnIndex = Math.ceil(endTotalMinutes / 60.0) - 1;
                                                        const startOffset =
                                                            columnOffsets[startColumnIndex] +
                                                            ((startTotalMinutes % 60) / 60.0) *
                                                            (columnOffsets[startColumnIndex + 1] - columnOffsets[startColumnIndex]);

                                                        const spanWidth = totalMinutes * (columnOffsets[endColumnIndex] - columnOffsets[endColumnIndex - 1]) / 60.0;

                                                        //console.log(`${schedule.Title}: duration=${totalMinutes} startOffset=${startOffset} spanWidth=${spanWidth}`);
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

