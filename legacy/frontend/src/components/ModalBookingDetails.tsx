import { blueDark, redDark } from "@ant-design/colors";
import { Button, Divider, Modal, Typography } from "antd";
import { Schedule } from "../Schedule";
import { useContext, useEffect, useState } from "react";
import AccountContext from "./AccountContext";

interface ModalBookingDetailsProps {
    isModalOpen: boolean;
    setIsModalOpen: React.Dispatch<React.SetStateAction<boolean>>;
    selectedSchedule: Schedule | null;
    handleDeleteSchedule: (schedule: Schedule) => Promise<string | null>;
    handleConfirmSchedule: (schedule: Schedule) => Promise<string | null>;
}

function ModalBookingDetails(props: ModalBookingDetailsProps) {

    const [deleting, setDeleting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        // console.log('Setting error null:', props.selectedSchedule);
        setError(null);
        setDeleting(false);
    }, [props.isModalOpen]);

    const [confirming, setConfirming] = useState(false);

    const accountContext = useContext(AccountContext);

    return (
        <Modal
            open={props.isModalOpen}
            onOk={() => props.setIsModalOpen(false)}
            onCancel={() => props.setIsModalOpen(false)}

            footer={[
                <Button type="primary" loading={deleting} danger key="delete" onClick={async () => {
                    setDeleting(true);
                    var result = await props.handleDeleteSchedule(props.selectedSchedule!);
                    if (result) {
                        console.error(`Delete booking failed: ` + result);
                        setError(result);
                        setDeleting(false);
                    }
                    else {
                        props.setIsModalOpen(false);
                    }
                }}>
                    Xóa lịch
                </Button>,
                <>
                    {(accountContext.isAdmin && props.selectedSchedule?.State == 1) &&
                        <Button type="primary" loading={confirming} key="confirm" onClick={async () => {
                            setConfirming(true);
                            var result = await props.handleConfirmSchedule(props.selectedSchedule!);
                            if (result) {
                                console.error(`Confirm booking failed: ` + result);
                                setError(result);
                                setConfirming(false);
                            }
                            else {
                                props.setIsModalOpen(false);
                            }
                            setConfirming(false);
                        }}>
                            Xác nhận lịch
                        </Button>
                    }
                </>,
                <Button type={accountContext.isAdmin ? "default" : "primary"} key="back" onClick={() => props.setIsModalOpen(false)}>
                    Đóng
                </Button>,
            ]}
        >
            <div style={{ height: '2rem' }}></div>
            <Typography.Title level={3} style={{ margin: 0 }}>
                Cuộc họp
                <span style={{ color: redDark.primary }}>
                    {' ' + props.selectedSchedule?.Title + ' '}
                </span>
                <br></br>
                {props.selectedSchedule && (() => {
                    const now = new Date();
                    const scheduleDate = new Date(props.selectedSchedule.Date);
                    const [startHour, startMinute] = props.selectedSchedule.StartTime.split(':').map(Number);
                    const [endHour, endMinute] = props.selectedSchedule.EndTime.split(':').map(Number);

                    scheduleDate.setHours(startHour, startMinute, 0, 0);
                    const scheduleEndDate = new Date(scheduleDate);
                    scheduleEndDate.setHours(endHour, endMinute, 0, 0);

                    if (now < scheduleDate) {
                        return 'sẽ ';
                    } else if (now >= scheduleDate && now <= scheduleEndDate) {
                        return 'đang ';
                    } else {
                        return 'đã ';
                    }
                })()}
                diễn ra tại phòng họp
                <span style={{ color: blueDark.primary }}>
                    {' R' + props.selectedSchedule?.Room + ' '}
                </span>
                <br></br>vào
                <span style={{ color: redDark.primary }}>
                    {' ' + props.selectedSchedule?.Date.toLocaleDateString('vi-VN', { weekday: 'long' })}, ngày {props.selectedSchedule?.Date.getDate()} tháng {(props.selectedSchedule?.Date.getMonth() ?? -1) + 1} năm {props.selectedSchedule?.Date.getFullYear()}
                </span>
                <br></br>từ lúc
                <span style={{ color: blueDark.primary }}>
                    {' ' + props.selectedSchedule?.StartTime + ' '}
                </span>

                đến
                <span style={{ color: blueDark.primary }}>
                    {' ' + props.selectedSchedule?.EndTime}
                </span>.
                <br></br>
                <Divider></Divider>
                Lịch họp này do
                <span style={{ color: redDark.primary }}>
                    {' ' + props.selectedSchedule?.Creator + ' '}
                </span>
                <br></br>
                (
                <span style={{ color: redDark.primary }}>
                    {props.selectedSchedule?.Name}
                </span>
                )
                tạo.

                {
                    props.selectedSchedule?.State === 1 &&
                    <span style={{ color: blueDark.primary }}>
                        {' Lịch họp này đang cần được xác nhận.'}
                    </span>
                }

                {
                    props.selectedSchedule?.Note != null ? (
                        <>
                            <br></br>
                            <Typography.Text strong>Ghi chú:</Typography.Text>
                            <blockquote style={{ marginTop: 0 }}>
                                <Typography.Text>{props.selectedSchedule?.Note}</Typography.Text>
                            </blockquote>
                        </>
                    ) : (
                        <>
                            <br></br>
                            <Typography.Text strong>Không có ghi chú.</Typography.Text>
                        </>
                    )
                }

                {
                    error != null &&
                    <>
                        <Divider></Divider>
                        <Typography.Text type="danger" style={{ fontSize: '0.8rem' }}>
                            {error}
                        </Typography.Text>
                    </>
                }
                <Divider></Divider>



            </Typography.Title>

        </Modal >
    );
}

export default ModalBookingDetails;