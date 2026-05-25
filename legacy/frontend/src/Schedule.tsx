export interface Schedule {
    Id: number;
    Creator: string; // owner email
    Name: string; // owner name
    Title: string;
    Room: number;
    Date: Date; // '2021-10-01'
    StartTime: string; // '15:30'
    EndTime: string; // '16:30'    
    State: number; // 0: booked, 1: confirmation wait, 2: unused, 254: confirmation rejected, 255: cancelled
    Note?: string ;
}

export interface WeekSchedule {
    startDate: Date;
    schedules: Schedule[];
}