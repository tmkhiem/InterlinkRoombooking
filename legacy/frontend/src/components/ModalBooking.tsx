import { DatePicker, Button, Typography, Modal, Form, Input, Select, InputNumber, Space, Divider } from 'antd';

import { useContext, useState } from 'react';
import dayjs from 'dayjs';
import { Schedule } from '../Schedule';
import { CheckCircleTwoTone } from '@ant-design/icons';
import AccountContext from './AccountContext';

const formItemLayout = {
    labelCol: {
        xs: { span: 24 },
        sm: { span: 6 },
    },
    wrapperCol: {
        xs: { span: 24 },
        sm: { span: 14 },
    },
};

interface ModalBookingProps {
    schedule: Schedule | null; // Schedule object containing booking details
    onSubmit: (schedule: Schedule) => Promise<void>; // Function to handle form submission as a promise
    onCancel: () => void; // Function to handle form cancellation
    open: boolean; // Modal open state
    error: string | null; // Error message to display in the modal
    setError: React.Dispatch<React.SetStateAction<string | null>>;
}

function ModalBooking(props: ModalBookingProps) {

    // Modal functions
    const [bookingForm] = Form.useForm(); // Form instance
    const [isFormValid, setIsFormValid] = useState(false); // State to track form validation

    const [selectedTitle, setSelectedTitle] = useState(''); // State for selected title
    const [selectedRoom, setSelectedRoom] = useState(2); // State for selected room
    const [selectedDate, setSelectedDate] = useState(dayjs(new Date())); // State for selected date

    const [selectedStartHour, setSelectedStartHour] = useState(8); // State for selected start hour
    const [selectedStartMinute, setSelectedStartMinute] = useState(0); // State for selected start minute
    const [selectedEndHour, setSelectedEndHour] = useState(9); // State for selected end hour
    const [selectedEndMinute, setSelectedEndMinute] = useState(0); // State for selected end minute
    const [confirmLoading, setConfirmLoading] = useState(false); // Loading state for the modal

    const handleFormSubmit = async () => {
        try {
            // Validate all fields
            const values = await bookingForm.validateFields();

            setConfirmLoading(true);

            // Create a new schedule object with the form values
            const newSchedule: Schedule = {
                Id: 0,
                Creator: accountContext.email,
                Name: accountContext.name,
                Title: values.title,
                Room: values.room,
                Date: values.date.format('YYYY-MM-DD'), // Format date to YYYY-MM-DD
                StartTime: `${selectedStartHour.toString().padStart(2, '0')}:${selectedStartMinute.toString().padStart(2, '0')}`,
                EndTime: `${selectedEndHour.toString().padStart(2, '0')}:${selectedEndMinute.toString().padStart(2, '0')}`,
                State: 1, // Confirmation waiting state
                Note: values.notes || null, // Use null if notes are empty
            };

            // console.log('New schedule selectedStartHour:', selectedStartHour);  
            // console.log('New schedule selectedStartMinute:', selectedStartMinute);
            // console.log('New schedule selectedEndHour:', selectedEndHour);
            // console.log('New schedule selectedEndMinute:', selectedEndMinute);
            // console.log('New schedule Values: ', values)
            // console.log('New schedule start time:', values.startTime[0].toString().padStart(2, '0'));
            // console.log('New schedule end time:', values.endTime[0].toString().padStart(2, '0'));
            // console.log('New schedule:', newSchedule);


            await props.onSubmit(newSchedule);

            setConfirmLoading(false); // Reset loading state
            bookingForm.resetFields();

        } catch (error) {
            console.error('Validation failed:', error);
        }
    };

    const handleFormReset = () => {
        bookingForm.resetFields(); // Reset all fields to default values
        setIsFormValid(false); // Reset form validation state
        props.setError(null); // Reset error state
    };

    const handleModalClose = () => {
        props.onCancel();
        bookingForm.resetFields(); // Clear the form when the modal is closed
        setIsFormValid(false); // Reset form validation state
    };

    const IsEndTimeLaterThanStartTime = (startHour: number, endHour: number, startMinute: number, endMinute: number) => {
        return startHour > endHour ||
            (startHour === endHour &&
                startMinute >= endMinute);
    }

    const meetingHoursValidator = (_0: any, _1: any) => {
        return IsEndTimeLaterThanStartTime(selectedStartHour, selectedEndHour, selectedStartMinute, selectedEndMinute)
            ? Promise.reject(new Error('Thời gian bắt đầu phải trước thời gian kết thúc.'))
            : Promise.resolve();
    }

    const accountContext = useContext(AccountContext);

    return (<Modal
        open={props.open}
        title="Đặt lịch"
        footer={[]}
        confirmLoading={confirmLoading}
        onClose={(e) => e.stopPropagation()}
        closable={false}
    >
        <Form {...formItemLayout}
            form={bookingForm}
            onFieldsChange={() => {
                console.log('------------------------------------');
                console.log('Form changed:', bookingForm.getFieldsValue());

                // manually validate all fields
                const title = bookingForm.getFieldValue('title');
                const room = bookingForm.getFieldValue('room');
                const date = bookingForm.getFieldValue('date');
                const startTime = bookingForm.getFieldValue('startTime');
                const endTime = bookingForm.getFieldValue('endTime');

                const startHour = startTime?.[0];
                const startMinute = startTime?.[1];
                const endHour = endTime?.[0];
                const endMinute = endTime?.[1];

                const isTimeOrderValid = !IsEndTimeLaterThanStartTime(startHour, endHour, startMinute, endMinute);

                if (title && room && date && startTime && endTime && isTimeOrderValid) {
                    // console.log('Form is valid!');
                    setIsFormValid(true);
                } else {
                    // console.log('Form is invalid!');
                    // console.log('title:', title);
                    // console.log('room:', room);
                    // console.log('date:', date);
                    // console.log('startTime:', startTime);
                    // console.log('endTime:', endTime);
                    // console.log('isTimeOrderValid:', isTimeOrderValid);
                    // console.log('isFormValid:', isFormValid);

                    setIsFormValid(false);
                }
            }}
            initialValues={() => {
                if (props.schedule) {
                    console.log('Initial values:', props.schedule);
                    return {
                        creator: accountContext.email,
                        name: accountContext.name,
                        title: props.schedule.Title,
                        room: props.schedule.Room,
                        date: dayjs(props.schedule.Date),
                        startTime: [props.schedule.StartTime.split(':')[0], props.schedule.StartTime.split(':')[1]],
                        endTime: [props.schedule.EndTime.split(':')[0], props.schedule.EndTime.split(':')[1]],
                        notes: props.schedule.Note
                    };
                }
                else {
                    console.log('Initial values: default', selectedTitle, selectedRoom, selectedDate, selectedStartHour, selectedStartMinute, selectedEndHour, selectedEndMinute);
                    return {
                        creator: accountContext.email,
                        name: accountContext.name,
                        title: selectedTitle,
                        room: selectedRoom,
                        date: selectedDate,
                        startTime: [selectedStartHour, selectedStartMinute],
                        endTime: [selectedEndHour, selectedEndMinute],
                        notes: null
                    };
                }
            }}
        >

            {/* Creator */}
            <Form.Item label="Tài khoản" className='modalAddBookingFormItem' name='creator'>
                <Typography.Text strong>{accountContext.email}</Typography.Text>
            </Form.Item>

            <Form.Item label="Tên" className='modalAddBookingFormItem' name='name'>
                <Typography.Text strong>{accountContext.name}</Typography.Text>
            </Form.Item>

            {/* Title */}
            <Form.Item
                label="Tiêu đề" name='title'
                className='modalAddBookingFormItem'
                rules={[
                    {
                        required: true,
                        message: 'Vui lòng nhập tiêu đề cuộc họp!',
                    },
                ]}
                validateStatus={selectedTitle ? 'success' : 'error'}
                help={selectedTitle ? null : 'Vui lòng nhập tiêu đề cuộc họp!'}
            >
                <Input
                    placeholder="Nhập tiêu đề cuộc họp"
                    value={selectedTitle}
                    onChange={(e) => setSelectedTitle(e.target.value)}
                />
            </Form.Item>

            {/* Room */}
            <Form.Item
                label="Phòng họp"
                className='modalAddBookingFormItem'
                name='room'
                help={selectedRoom === 1 ? 'Phòng họp R1 sẽ cần được duyệt bởi bộ phận HCNS.' : null} // Warning message
                validateStatus={selectedRoom === 1 ? 'validating' : undefined} // Warning status
                initialValue={2}
                rules={[
                    {
                        required: true,
                        message: 'Vui lòng chọn phòng họp!',
                    },
                ]}
            >
                <Select
                    value={selectedRoom} // Controlled value
                    onChange={(value) => setSelectedRoom(value)} // Update state on change
                    style={{ width: '10rem' }}
                    options={[
                        { value: 1, label: 'R1 (tầng trệt)' },
                        { value: 2, label: 'R2 (tầng 1)' },
                        { value: 3, label: 'R3 (tầng 5)' },
                    ]}
                />
            </Form.Item>

            {/* Date */}
            <Form.Item
                label="Ngày"
                name='date'
                className='modalAddBookingFormItem'
                initialValue={dayjs(new Date())}
                rules={[
                    {
                        required: true,
                        message: 'Vui lòng chọn ngày họp!',
                    },
                ]}
            >
                <DatePicker
                    format={'DD/MM/YYYY'}
                    value={selectedDate}
                    onChange={(date) => {
                        setSelectedDate(date || dayjs(new Date()));
                    }}
                />
            </Form.Item>

            {/* Start time */}
            <Form.Item
                label="Họp từ"
                name='startTime'
                className="modalAddBookingFormItem"
                initialValue={[selectedStartHour, selectedStartMinute]}
                help={
                    IsEndTimeLaterThanStartTime(selectedStartHour, selectedEndHour, selectedStartMinute, selectedEndMinute)
                        ? 'Thời gian bắt đầu phải trước thời gian kết thúc.'
                        : null
                } // Warning message
                validateStatus={
                    IsEndTimeLaterThanStartTime(selectedStartHour, selectedEndHour, selectedStartMinute, selectedEndMinute)
                        ? 'error'
                        : undefined
                } // Error status
                rules={[
                    {
                        required: true,
                        message: 'Vui lòng chọn thời gian bắt đầu cuộc họp!',
                    },
                    {
                        validator: meetingHoursValidator,
                    },
                ]}
            >
                <Space>
                    <InputNumber
                        name='startHour'
                        min={7} max={20}
                        formatter={(value) => value?.toString().padStart(2, '0') as unknown as string}
                        value={selectedStartHour}
                        onChange={(value) => {
                            const hour = value || 8;
                            setSelectedStartHour(hour);
                            bookingForm.setFieldsValue({
                                startTime: [hour, selectedStartMinute],
                            });
                            bookingForm.validateFields();
                        }}
                        style={{ width: '4rem' }} />

                    <Typography.Text>giờ</Typography.Text>

                    <InputNumber
                        name='startMinute'
                        min={0} max={59}
                        formatter={(value) => value?.toString().padStart(2, '0') as unknown as string}
                        value={selectedStartMinute}
                        onChange={(value) => {
                            const minute = value || 0;
                            setSelectedStartMinute(minute);
                            bookingForm.setFieldsValue({
                                startTime: [selectedStartHour, minute],
                            });
                            bookingForm.validateFields();
                        }}
                        style={{ width: '4rem' }} />

                    <Typography.Text>phút</Typography.Text>

                </Space>
            </Form.Item>

            {/* End time */}
            <Form.Item
                label="Họp tới"
                name='endTime'
                className='modalAddBookingFormItem'
                initialValue={[selectedEndHour, selectedEndMinute]}
                help={
                    IsEndTimeLaterThanStartTime(selectedStartHour, selectedEndHour, selectedStartMinute, selectedEndMinute)
                        ? 'Thời gian bắt đầu phải trước thời gian kết thúc.'
                        : null
                } // Warning message
                validateStatus={
                    IsEndTimeLaterThanStartTime(selectedStartHour, selectedEndHour, selectedStartMinute, selectedEndMinute)
                        ? 'error'
                        : undefined
                } // Error status
                rules={[
                    {
                        required: true,
                        message: 'Vui lòng chọn thời gian kết thúc cuộc họp!',
                    },
                    {
                        validator: meetingHoursValidator
                    },
                ]}
            >
                <Space>
                    <InputNumber
                        name='endHour'
                        min={7} max={21}
                        formatter={(value) => value?.toString().padStart(2, '0') as unknown as string}
                        value={selectedEndHour}
                        onChange={(value) => {
                            const hour = value || selectedEndHour;
                            setSelectedEndHour(hour);
                            bookingForm.setFieldsValue({
                                endTime: [hour, selectedEndMinute],
                            });
                            bookingForm.validateFields();
                        }}
                        style={{ width: '4rem' }} />

                    <Typography.Text>giờ</Typography.Text>


                    <InputNumber
                        name='endMinute'
                        min={0} max={59}
                        formatter={(value) => value?.toString().padStart(2, '0') as unknown as string}
                        value={selectedEndMinute}
                        onChange={(value) => {
                            const minute = value || selectedEndMinute;
                            setSelectedEndMinute(minute);
                            bookingForm.setFieldsValue({
                                endTime: [selectedEndHour, minute],
                            });
                            bookingForm.validateFields();
                        }}
                        style={{ width: '4rem' }} />

                    <Typography.Text>phút</Typography.Text>

                </Space>
            </Form.Item>

            {/* Notes */}
            <Form.Item
                label="Ghi chú:" name='notes'
                className='modalAddBookingFormItem'
            >
                <Input.TextArea
                    placeholder="Ghi chú thêm cho cuộc họp (nếu có), như yêu cầu chuẩn bị hoặc đặt cho ai khác."
                    value={selectedTitle}
                    onChange={(e) => setSelectedTitle(e.target.value)}
                    rows={4}
                />
            </Form.Item>

            {
                props.error != null &&
                <>
                    <Divider style={{ marginTop: '0.5rem', marginBottom: '0.5rem' }} />
                    <Typography.Text type='danger' style={{ fontSize: '0.8rem' }}>
                        {props.error}
                    </Typography.Text>
                </>
            }

            <Divider style={{ marginTop: '0.5rem', marginBottom: '0.5rem' }} />

            <Typography.Text type='secondary' style={{ fontSize: '0.8rem' }}>
                Hãy kiểm tra lại thông tin trước khi đặt lịch. Nếu không sự dụng, vui lòng hủy lịch để người khác có thể sử dụng.
            </Typography.Text>

            <div
                className='modalAddBookingFormItem'
                style={{
                    display: 'flex',
                    justifyContent: 'end',
                    gap: '1rem',
                    marginTop: '1rem',
                }}
            >
                <Button onClick={handleFormReset}>Nhập lại</Button>
                <Button onClick={handleModalClose} color="danger" variant="outlined">Huỷ</Button>
                <Button type="primary" htmlType="submit" onClick={handleFormSubmit} loading={confirmLoading} disabled={!isFormValid}>
                    <CheckCircleTwoTone />Đặt lịch
                </Button>

            </div>
        </Form>

    </Modal>
    );
}

export default ModalBooking;