-- Patient Management System - Database Initialization Script

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

-- Seed data
INSERT INTO patients (name, gender, date_of_birth, phone, address) VALUES
    ('张伟', '男', '1985-03-12', '13800138001', '北京市朝阳区建国路88号'),
    ('李娜', '女', '1990-07-25', '13900139002', '上海市浦东新区张江高科技园区'),
    ('王芳', '女', '1978-11-08', '13700137003', '广州市天河区体育西路103号'),
    ('刘洋', '男', '1995-01-30', '13600136004', '深圳市南山区科技园南区'),
    ('陈静', '女', '1988-09-15', '13500135005', NULL);
