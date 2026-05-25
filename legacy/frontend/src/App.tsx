import './App.css';
import './components/WeekHorizontal.css'; // Add a CSS file for styling

import { DatePicker, Layout, Button, Card, Typography, Flex, Avatar, MenuProps, Dropdown, Divider, ConfigProvider } from 'antd';
const { Title } = Typography;

import WeekHorizontal from './components/WeekHorizontal';
import type { Dayjs } from 'dayjs';
import { useEffect, useState } from 'react';
import dayjs from 'dayjs';
import isoWeek from 'dayjs/plugin/isoWeek';
import { Schedule, WeekSchedule } from './Schedule';
import ModalBooking from './components/ModalBooking';
import AccountContext, { Account, NullAccount } from './components/AccountContext';
import Logo from './logo-header.svg';

dayjs.extend(isoWeek);

const { Header, Content, Footer } = Layout;

const MaterialIcon = ({ name }: { name: string }) => (
  <span className="material-symbols-outlined app-icon" aria-hidden="true">
    {name}
  </span>
);

const contentStyle: React.CSSProperties = {
  textAlign: 'center',
  minHeight: 120,
  height: '100%',
  color: 'black',
  overflow: 'auto',
};

const pageLayoutStyle: React.CSSProperties = {
  overflow: 'hidden',
  height: '100vh',
};

async function GETSchedule(startDate: Date): Promise<Schedule[]> {
  // Convert `startDate` to 'YYYY-MM-DD' format
  const strStartDate = dayjs(startDate).format('YYYY-MM-DD');

  // Send GET Request to /api/schedules
  const response = await fetch(`/api/schedules?date=${strStartDate}`, {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
    },
  });
  if (!response.ok) {
    throw new Error('Network response was not ok');
  }
  const data = await response.json();

  // Convert the date strings to Date objects, change startTime and EndTime to format 'HH:mm'
  data.forEach((schedule: Schedule) => {
    schedule.Date = new Date(schedule.Date); // Convert date string to Date object
    schedule.StartTime = schedule.StartTime.slice(0, 5); // Keep only the first 5 characters (HH:mm)
    schedule.EndTime = schedule.EndTime.slice(0, 5); // Keep only the first 5 characters (HH:mm)
  });

  console.log('GET schedules:', data);

  return Promise.resolve(data as Schedule[]);
}

async function POSTSchedule(schedule: Schedule): Promise<string | null> {
  // Send POST Request to /api/schedules
  const response = await
    fetch('/api/schedule', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(schedule)
    })
      .catch((error) => {
        console.error('Error:', error);
        return Promise.resolve(error);
      });

  if (!response.ok)
    return Promise.resolve(response.text());
  else
    return Promise.resolve(null);
}

async function DELETESchedule(schedule: Schedule): Promise<string | null> {
  // Send DELETE Request to /api/schedules
  const response = await
    fetch(`/api/schedule/${schedule.Id}`, {
      method: 'DELETE',
      headers: {
        'Content-Type': 'application/json',
      },
    })
      .catch((error) => {
        console.error('Error:', error);
        return Promise.resolve(error);
      });

  if (!response.ok)
    return Promise.resolve(response.text());
  else
    return Promise.resolve(null);
}

async function GETAdmin(): Promise<Account> {
  // Send GET request to /api/self, read "true" or "false" from response body
  const response = await fetch(`/api/self`, {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
    },
  }).catch((error) => {
    console.error('Error:', error);
    return Promise.resolve(error);
  });

  if (!response.ok) {
    throw new Error('Network response was not ok');
  }
  const data = await response.json();
  return Promise.resolve(data as Account);
}

const PUTConfirmBooking = async (schedule: Schedule) => {
  const response = await fetch(`/api/confirm/${schedule.Id}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(schedule),
  });

  if (!response.ok) {
    throw new Error('Failed to confirm booking');
  }

  return null;
}

function App() {
  // Helper functions  
  const findLastMonday = (date: Date): Date => {
    const dayOfWeek = date.getDay(); // 0 = Sunday, 1 = Monday, ..., 6 = Saturday
    if (dayOfWeek === 1) {
      return date; // If it's already Monday, return the same date
    }
    const lastMonday = new Date(date);
    lastMonday.setDate(date.getDate() - (dayOfWeek === 0 ? 6 : dayOfWeek - 1));
    return lastMonday;
  }

  const calculateWeekOfYear = (date: Date): number => {
    return dayjs(date).isoWeek();
  };

  const calculateYearOfWeek = (date: Date): number => {
    return dayjs(date).isoWeekYear();
  }

  // State variables
  const [startDate, setStartDate] = useState(findLastMonday(new Date()));

  const [weekSchedule, setWeekSchedule] = useState<WeekSchedule>({
    startDate: startDate,
    schedules: [
      {
        Id: 1,
        Title: 'Họp 1',
        Creator: 'anv@email.com',
        Name: 'Nguyễn Văn A',
        Room: 1,
        Date: new Date('2025-03-18'),
        StartTime: '8:00',
        EndTime: '17:00',
        State: 0,
      }
    ],
  });

  const [formAddBookingOpen, setFormAddBookingOpen] = useState(false);

  const [modalError, setModalError] = useState<string | null>(null);

  // Event handlers
  const datePickerValueChangedHandler = (date: Dayjs | (Dayjs | null)[] | null, dateString: string | string[]) => {
    console.log(date, dateString);
    if (date && !Array.isArray(date)) {
      const dayOfWeek = date.day(); // 0 = Sunday, 1 = Monday, ..., 6 = Saturday
      if (dayOfWeek !== 1) {
        const nearestMonday = date.subtract(dayOfWeek === 0 ? 6 : dayOfWeek - 1, 'day');
        setStartDate(nearestMonday.toDate());
      } else {
        setStartDate(date.toDate());
      }
    }
  };

  const buttonPreviousWeekClickHandler = () => {
    const previousWeek = dayjs(startDate).subtract(1, 'week').toDate();
    setStartDate(previousWeek);
  };

  const buttonNextWeekClickHandler = () => {
    const nextWeek = dayjs(startDate).add(1, 'week').toDate();
    setStartDate(nextWeek);
  };

  const [account, setAccount] = useState(NullAccount);

  const refreshSchedule = (startDate: Date) => {
    GETSchedule(startDate).then((schedules) => {
      const weekStartDate = findLastMonday(startDate);
      const weekSchedules = schedules;
      setWeekSchedule({
        startDate: weekStartDate,
        schedules: weekSchedules,
      });
    });

    GETAdmin().then((account) => {
      //debugger;            
      setAccount(account);
    }
    ).catch((error) => {
      console.error('Error fetching admin status:', error);

      // Redirect to /api/signin
      window.location.href = '/api/signin';
    });
  }

  const handleDeleteSchedule = async (schedule: Schedule) => {
    setModalError(null);
    var result = await DELETESchedule(schedule);
    setModalError(result);

    if (result) {
      console.error(`Delete booking failed: ` + result);
      return Promise.resolve(result);
    } else {
      console.log('Delete booking successful!');
      refreshSchedule(startDate);
    }

    return Promise.resolve(null);
  };

  const handleConfirmSchedule = async (schedule: Schedule) => {
    setModalError(null);
    var result = await PUTConfirmBooking(schedule);
    setModalError(result);

    if (result) {
      console.error(`Confirm booking failed: ` + result);
      return Promise.resolve(result);
    } else {
      console.log('Confirm booking successful!');
      refreshSchedule(startDate);
    }

    return Promise.resolve(null);
  };

  const headerStyle: React.CSSProperties = {
    textAlign: 'center',
    color: '#fff',
    height: 64,
    paddingInline: 48,
    paddingTop: '1rem',
    paddingBottom: '1rem',
    // backgroundColor: account.isAdmin ? '#005c9a' : '#d71920',
    backgroundColor: 'transparent',
    gap: '1rem 1rem',
    display: 'flex',
    flexGrow: 1,
  };

  const accountMenuItems: MenuProps['items'] = [
    {
      key: '1',
      label: (
        <Typography.Text strong >
          {account.name}
        </Typography.Text>
      ),
    },
    {
      key: '2',
      type: 'divider',
    },
    {
      key: '3',
      label: (
        <a href="/api/signout" style={{ textDecoration: 'none' }}>
          <Typography.Text strong style={{ color: 'red' }}>
            Đăng xuất
          </Typography.Text>
        </a>
      ),
    },
  ];


  // Fetch schedule when the component mounts and when the startDate changes
  useEffect(() => refreshSchedule(startDate), [startDate]);

  return (
    <AccountContext.Provider value={account}>

      <ConfigProvider
        theme={{
          token: {
            colorPrimary: '#6750a4',
            borderRadius: 14,
            fontFamily: '"Inter", system-ui, -apple-system, sans-serif',
            colorBgLayout: '#f5f2ff',
          },
          components: {
            Card: {
              colorBgContainer: '#ffffff',
            },
          },
        }}
      >
        <Layout style={pageLayoutStyle}>
        <Header style={{
          display: 'flex',
          padding: 0,
          background: account.isAdmin ?
            'linear-gradient(130deg, rgba(215,25,32,1) 35%, rgba(0,92,154,1) 100%)' :
            'linear-gradient(130deg, rgba(0,92,154,1) 35%, rgba(215,25,32,1) 100%)',
        }}>
          <img src={Logo} style={{ maxWidth: '250px', background: 'transparent' }}></img>
          <div style={headerStyle}>
            {/* Week picker */}
            <Button onClick={buttonPreviousWeekClickHandler} icon={<MaterialIcon name="chevron_left" />}>Tuần trước</Button>
            <DatePicker
              onChange={datePickerValueChangedHandler}
              picker="week"
              placeholder="Chọn tuần"
              format={'wo (YYYY)'}
              value={dayjs(startDate)}
            />
            <Button onClick={buttonNextWeekClickHandler} icon={<MaterialIcon name="chevron_right" />} iconPosition='end'>Tuần sau</Button>
            {/* Empty space to center the title */}
            <div style={{ flex: 1 }}></div>

            {/* Week number */}
            <Flex vertical justify='center'>
              <Typography>
                <Title level={4} style={
                  {
                    color: 'white',
                    verticalAlign: 'middle',
                    margin: 0,
                  }
                }>Tuần {(calculateWeekOfYear(startDate))} năm {calculateYearOfWeek(startDate)}</Title>
              </Typography>
            </Flex>

            <Dropdown menu={{ items: accountMenuItems }}>
              <a onClick={(e) => e.preventDefault()} style={{ lineHeight: '0' }}>
                <Avatar style={{ background: account.isAdmin ? '#d71920' : '#005c9a' }} icon={<MaterialIcon name="person" />} />
              </a>
            </Dropdown>
          </div>
        </Header>

        <Layout>
          <Content style={contentStyle}>
            <Card
              title="Lịch tuần"
              extra={
                <Flex justify='space-between' align='center' gap='1rem' vertical={false}>

                  <Typography.Text strong>
                    Chú thích:
                  </Typography.Text>
                  <div className='schedule-item room-1' color='green' >Phòng R1 (trệt)</div>
                  <div className='schedule-item room-2' color='green' >Phòng R2 (T.1)</div>
                  <div className='schedule-item room-3' color='green' >Phòng R3 (T.5)</div>
                  <div className='schedule-item room-1 state-1' color='green' >Chưa duyệt</div>
                  <Divider type='vertical' />
                  <Button onClick={() => setFormAddBookingOpen(true)} icon={<MaterialIcon name="add_circle" />}>Đặt lịch</Button>

                </Flex>
              }
              style={{
                margin: '1rem',
                borderRadius: 8,
                boxShadow: '0 1rem 1rem rgba(0,0,0,0.5)',
                minWidth: 1200,
              }}
            >

              <ModalBooking
                schedule={null}
                open={formAddBookingOpen}
                onCancel={() => setFormAddBookingOpen(false)}
                onSubmit={async (value) => {

                  setModalError(null);
                  var result = await POSTSchedule(value);
                  setModalError(result);

                  if (result) {
                    console.error(`Add booking failed: ` + result);
                  } else {
                    console.log('Booking successful!');
                    setFormAddBookingOpen(false);
                    refreshSchedule(startDate);
                  }

                  return Promise.resolve();
                }}
                error={modalError}
                setError={setModalError}

              ></ModalBooking>

              <WeekHorizontal
                weekSchedule={weekSchedule}
                onDeleteSchedule={handleDeleteSchedule}
                onConfirmSchedule={handleConfirmSchedule}
              />
            </Card>
          </Content>
        </Layout>

        <Footer
          style={{
            position: 'fixed',
            bottom: 0,
            left: 0,
            right: 0,
            textAlign: 'center',
            padding: '1rem',
            backgroundColor: 'transparent',
          }}>
          <Typography.Text type="secondary">RoomBooking 0.1.0, tmkhiem 2025</Typography.Text>
        </Footer>
      </Layout >
      </ConfigProvider>
    </AccountContext.Provider>
  );
}

export default App;
