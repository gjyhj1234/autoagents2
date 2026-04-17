-- Patient Management Demo
-- PostgreSQL initialization script

CREATE TABLE IF NOT EXISTS patients (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    gender VARCHAR(10) NOT NULL,
    date_of_birth DATE NOT NULL,
    phone VARCHAR(20) NOT NULL,
    address VARCHAR(200),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed data: sample patients
INSERT INTO patients (id, name, gender, date_of_birth, phone, address)
VALUES
    (gen_random_uuid(), '张伟', 'Male',   '1985-03-12', '13800138001', '北京市朝阳区建国路88号'),
    (gen_random_uuid(), '李娜', 'Female', '1990-07-25', '13900139002', '上海市浦东新区世纪大道100号'),
    (gen_random_uuid(), '王芳', 'Female', '1978-11-08', '13700137003', '广州市天河区天河路385号'),
    (gen_random_uuid(), '刘洋', 'Male',   '2000-01-30', '13600136004', '成都市武侯区人民南路四段11号'),
    (gen_random_uuid(), '陈静', 'Female', '1995-06-17', '13500135005', NULL);
